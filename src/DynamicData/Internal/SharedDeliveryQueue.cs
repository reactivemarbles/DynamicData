// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A delivery queue that serializes delivery across multiple sources with different
/// item types. Each source gets a typed <see cref="DeliverySubQueue{T}"/> via
/// <see cref="CreateQueue{T}"/>, which holds that source's notifications without
/// boxing them. A single order queue records which source each pending notification
/// came from, so delivery follows the order notifications were received rather than
/// the order the sources happen to be registered in.
/// <para>
/// The lock is never held while an observer runs. A producer that arrives while
/// another thread is delivering enqueues and returns rather than blocking, so a
/// pipeline that crosses into another cache during delivery cannot deadlock against
/// a producer on this one.
/// </para>
/// <para>
/// A notification raised synchronously by an observer, on the thread that is delivering,
/// is delivered inline before that observer returns, as it would be under a reentrant
/// lock. Notifications other threads queued in the meantime still wait their turn, so an
/// observer never sees another thread's notification in the middle of its own delivery,
/// and is never itself re-entered.
/// </para>
/// </summary>
internal sealed class SharedDeliveryQueue : IDisposable
{
    /// <summary>
    /// One entry per pending notification, identifying the source it belongs to,
    /// in the order the notifications were received. The payloads themselves stay
    /// in their typed sub-queues, so recording the order costs no allocation.
    /// </summary>
    private readonly Queue<DrainableBase> _order = new();

#if NET9_0_OR_GREATER
    private readonly Lock _gate;
#else
    private readonly object _gate;
#endif

    private int _drainThreadId = -1;
    private volatile bool _isTerminated;

    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class with its own internal lock.</summary>
    public SharedDeliveryQueue()
    {
#if NET9_0_OR_GREATER
        _gate = new Lock();
#else
        _gate = new object();
#endif
    }

#if NET9_0_OR_GREATER
    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class with a caller-provided lock.</summary>
    public SharedDeliveryQueue(Lock gate) => _gate = gate;
#else
    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class with a caller-provided lock.</summary>
    public SharedDeliveryQueue(object gate) => _gate = gate;
#endif

    /// <summary>Gets a value indicating whether this queue has been terminated.</summary>
    public bool IsTerminated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isTerminated;
    }

    /// <summary>Creates a typed sub-queue bound to the specified observer.</summary>
    public DeliverySubQueue<T> CreateQueue<T>(IObserver<T> observer) => new(this, observer);

    /// <summary>Acquires the gate for read-only inspection. Does not trigger delivery on dispose.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyScopedAccess AcquireReadLock() => new(this);

    /// <summary>
    /// Terminates the queue, rejecting further enqueues, and blocks until any in-flight
    /// delivery has completed. After this returns, no more observer callbacks will fire.
    /// Safe to call from within a delivery callback, which skips the spin-wait.
    /// </summary>
    public void Dispose()
    {
        EnterLock();

        _isTerminated = true;
        _order.Clear();

        if (_drainThreadId == Environment.CurrentManagedThreadId)
        {
            ExitLock();
            return;
        }

        ExitLock();

        SpinWait spinner = default;
        while (Volatile.Read(ref _drainThreadId) != -1)
            spinner.SpinOnce();
    }

    /// <summary>Records that the given source has one more notification pending. Must be called under the lock.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnqueueOrder(DrainableBase source) => _order.Enqueue(source);

#if NET9_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnterLock() => _gate.Enter();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitLock() => _gate.Exit();
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnterLock() => Monitor.Enter(_gate);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitLock() => Monitor.Exit(_gate);
#endif

    /// <summary>
    /// Releases the lock after <paramref name="source"/> has enqueued, and delivers.
    /// Must be called under the lock.
    /// </summary>
    /// <param name="source">The sub-queue that just enqueued.</param>
    internal void ExitLockAndDrain(DrainableBase source)
    {
        var currentThreadId = Environment.CurrentManagedThreadId;

        // Same-thread reentrant: an observer on this thread raised the notification while it was
        // being delivered, so it is delivered inline, as Synchronize(lock) re-entrancy did. Only
        // this source is delivered: anything else at the head of the order queue was queued by
        // another thread, and delivering it here would hand that observer a notification in the
        // middle of the delivery that is still running.
        if (_drainThreadId == currentThreadId)
        {
            var inlineCount = _isTerminated ? 0 : source.InlineDeliverableCount;
            ExitLock();
            DeliverInline(source, inlineCount);
            return;
        }

        var shouldDrain = false;
        if (_drainThreadId == -1 && !_isTerminated && _order.Count != 0)
        {
            _drainThreadId = currentThreadId;
            shouldDrain = true;
        }

        ExitLock();

        if (shouldDrain)
        {
            DrainAll();
        }
    }

    private void DrainAll()
    {
        try
        {
            while (true)
            {
                if (!DrainPending())
                {
                    ReleaseDrainOwnership();
                    return;
                }

                // Atomically re-check for work and release ownership if there is none. Checking
                // and releasing in separate lock scopes would let a producer enqueue in between,
                // see that a drain is in progress, and rely on us to deliver an item we never saw.
                EnterLock();

                if (_order.Count != 0 && !_isTerminated)
                {
                    ExitLock();
                    continue;
                }

                _drainThreadId = -1;
                ExitLock();
                return;
            }
        }
        catch
        {
            ReleaseDrainOwnership();
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseDrainOwnership()
    {
        EnterLock();
        _drainThreadId = -1;
        ExitLock();
    }

    /// <summary>
    /// Delivers pending notifications, one at a time, in the order they were received.
    /// Each is delivered outside the lock.
    /// </summary>
    /// <returns>True if the queue drained normally; false if it was terminated.</returns>
    private bool DrainPending()
    {
        while (true)
        {
            EnterLock();

            if (_isTerminated)
            {
                ExitLock();
                return false;
            }

            if (_order.Count == 0)
            {
                ExitLock();
                return true;
            }

            var source = _order.Dequeue();

            // The entry is stale if its notification was already delivered inline, or if the source
            // has been disposed since, which drops its pending notifications. Skip it.
            if (!source.TryStageNext())
            {
                ExitLock();
                continue;
            }

            if (!ExitLockAndDeliverStaged(source))
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Delivers the oldest <paramref name="count"/> pending notifications of a single source,
    /// for a notification raised reentrantly on the delivering thread. They include any the
    /// source queued earlier from other threads, which have to precede it to keep the source's
    /// own order.
    /// </summary>
    /// <param name="source">The source to deliver from.</param>
    /// <param name="count">The number of notifications to deliver.</param>
    private void DeliverInline(DrainableBase source, int count)
    {
        for (var i = 0; i < count; i++)
        {
            EnterLock();

            if (_isTerminated || !source.TryStageInline())
            {
                ExitLock();
                return;
            }

            if (!ExitLockAndDeliverStaged(source))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Releases the lock and delivers the notification <paramref name="source"/> has staged,
    /// terminating the queue if it was an error. Must be called under the lock.
    /// </summary>
    /// <param name="source">The source holding the staged notification.</param>
    /// <returns>True if delivery can continue; false if the queue was terminated.</returns>
    private bool ExitLockAndDeliverStaged(DrainableBase source)
    {
        var isError = source.IsStagedError;

        ExitLock();

        source.DeliverStaged();

        if (!isError)
        {
            return true;
        }

        EnterLock();
        _isTerminated = true;
        _order.Clear();
        ExitLock();
        return false;
    }

    /// <summary>Read-only scoped access. Disposing releases the gate without triggering delivery.</summary>
    public ref struct ReadOnlyScopedAccess
    {
        private SharedDeliveryQueue? _owner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyScopedAccess(SharedDeliveryQueue owner)
        {
            _owner = owner;
            owner.EnterLock();
        }

        /// <summary>Gets a value indicating whether any notification is pending or in flight.</summary>
        public readonly bool HasPending
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _owner is not null && (_owner._drainThreadId != -1 || _owner._order.Count != 0);
        }

        /// <summary>Releases the gate lock.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
            {
                return;
            }

            _owner = null;
            owner.ExitLock();
        }
    }
}

/// <summary>Base class for typed sub-queues, so the drain loop can hold them without knowing their element type.</summary>
internal abstract class DrainableBase
{
    /// <summary>Gets a value indicating whether the staged notification is an error.</summary>
    internal abstract bool IsStagedError { get; }

    /// <summary>
    /// Gets how many pending notifications a reentrant delivery on the draining thread can deliver
    /// inline: all of them, unless this source is the one being delivered, whose observer must not
    /// be re-entered. Must be read under the lock.
    /// </summary>
    internal abstract int InlineDeliverableCount { get; }

    /// <summary>
    /// Moves the next pending notification into staging for the drain loop, which has just taken
    /// one of this source's order entries. Returns false if there is nothing to stage, including
    /// when the entry belongs to a notification that was already delivered inline.
    /// </summary>
    internal abstract bool TryStageNext();

    /// <summary>
    /// Moves the next pending notification into staging for inline delivery, ahead of its order
    /// entry, which the drain loop then skips. Returns false if there is nothing to stage.
    /// </summary>
    internal abstract bool TryStageInline();

    /// <summary>Delivers the staged notification to the observer.</summary>
    internal abstract void DeliverStaged();
}

/// <summary>
/// A typed sub-queue. Notifications are held as structs, so queuing one costs no
/// allocation. All enqueue access goes through <see cref="ScopedAccess"/>, which
/// acquires the parent's lock.
/// </summary>
internal sealed class DeliverySubQueue<T> : DrainableBase, IObserver<T>, IDisposable
{
    private readonly Queue<Notification<T>> _items = new(1);
    private readonly SharedDeliveryQueue _parent;
    private readonly IObserver<T> _observer;
    private Notification<T> _staged;
    private int _inlineDeliveredCount;
    private bool _isDelivering;
    private bool _isRemoved;

    internal DeliverySubQueue(SharedDeliveryQueue parent, IObserver<T> observer)
    {
        _parent = parent;
        _observer = observer;
    }

    /// <inheritdoc/>
    internal override bool IsStagedError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _staged.IsError;
    }

    /// <inheritdoc/>
    internal override int InlineDeliverableCount => _isRemoved || _isDelivering ? 0 : _items.Count;

    /// <summary>Acquires the parent gate. Disposing releases the lock and triggers delivery.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ScopedAccess AcquireLock() => new(this);

    /// <summary>Enqueues an OnNext notification via the lock, then delivers.</summary>
    public void OnNext(T value)
    {
        using var scope = AcquireLock();
        scope.EnqueueNext(value);
    }

    /// <summary>Enqueues an OnError notification via the lock, then delivers.</summary>
    public void OnError(Exception error)
    {
        using var scope = AcquireLock();
        scope.EnqueueError(error);
    }

    /// <summary>Enqueues an OnCompleted notification via the lock, then delivers.</summary>
    public void OnCompleted()
    {
        using var scope = AcquireLock();
        scope.EnqueueCompleted();
    }

    /// <summary>
    /// Marks this sub-queue as removed under the parent lock and drops its pending
    /// notifications. Any order entries left behind are skipped when the drain reaches
    /// them. Idempotent.
    /// </summary>
    public void Dispose()
    {
        _parent.EnterLock();
        try
        {
            if (_isRemoved)
            {
                return;
            }

            _isRemoved = true;
            _items.Clear();
        }
        finally
        {
            _parent.ExitLock();
        }
    }

    /// <inheritdoc/>
    internal override bool TryStageNext()
    {
        // Inline delivery takes this source's oldest notifications, so the oldest order entries
        // are the ones left without a notification.
        if (_inlineDeliveredCount != 0)
        {
            _inlineDeliveredCount--;
            return false;
        }

        return TryStage();
    }

    /// <inheritdoc/>
    internal override bool TryStageInline()
    {
        if (!TryStage())
        {
            return false;
        }

        _inlineDeliveredCount++;
        return true;
    }

    /// <inheritdoc/>
    internal override void DeliverStaged()
    {
        _isDelivering = true;
        try
        {
            _staged.Accept(_observer);
        }
        finally
        {
            _staged = default;
            _isDelivering = false;
        }
    }

    private bool TryStage()
    {
        if (_isRemoved || _items.Count == 0)
        {
            return false;
        }

        _staged = _items.Dequeue();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnqueueItem(Notification<T> item)
    {
        if (_parent.IsTerminated || _isRemoved)
        {
            return;
        }

        _items.Enqueue(item);
        _parent.EnqueueOrder(this);
    }

    /// <summary>Scoped access for enqueueing notifications. Acquires the parent's gate lock.</summary>
    public ref struct ScopedAccess
    {
        private DeliverySubQueue<T>? _owner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ScopedAccess(DeliverySubQueue<T> owner)
        {
            _owner = owner;
            owner._parent.EnterLock();
        }

        /// <summary>Enqueues an OnNext notification.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void EnqueueNext(T item) => _owner?.EnqueueItem(Notification<T>.CreateNext(item));

        /// <summary>Enqueues a terminal error.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void EnqueueError(Exception error) => _owner?.EnqueueItem(Notification<T>.CreateError(error));

        /// <summary>Enqueues a terminal completion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void EnqueueCompleted() => _owner?.EnqueueItem(Notification<T>.CreateCompleted());

        /// <summary>Releases the parent gate lock and delivers pending notifications.</summary>
        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
            {
                return;
            }

            _owner = null;
            owner._parent.ExitLockAndDrain(owner);
        }
    }
}
