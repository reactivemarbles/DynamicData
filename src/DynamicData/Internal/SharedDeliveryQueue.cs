// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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

    internal void ExitLockAndDrain()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;

        // Same-thread reentrant: if we're already draining on this thread, deliver newly
        // enqueued items inline. This preserves the same delivery order as Synchronize(lock):
        // child items emitted synchronously during parent delivery are delivered immediately,
        // not deferred.
        if (_drainThreadId == currentThreadId)
        {
            ExitLock();
            DrainPending();
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

            // The source may have been disposed since this entry was recorded, which drops
            // its pending notifications. Skip the stale entry and take the next one.
            if (!source.TryStageNext())
            {
                ExitLock();
                continue;
            }

            var isError = source.IsStagedError;

            ExitLock();

            source.DeliverStaged();

            if (isError)
            {
                EnterLock();
                _isTerminated = true;
                _order.Clear();
                ExitLock();
                return false;
            }
        }
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

    /// <summary>Moves the next pending notification into staging. Returns false if there is nothing to stage.</summary>
    internal abstract bool TryStageNext();

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
        if (_isRemoved || _items.Count == 0)
        {
            return false;
        }

        _staged = _items.Dequeue();
        return true;
    }

    /// <inheritdoc/>
    internal override void DeliverStaged()
    {
        _staged.Accept(_observer);
        _staged = default;
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
            owner._parent.ExitLockAndDrain();
        }
    }
}