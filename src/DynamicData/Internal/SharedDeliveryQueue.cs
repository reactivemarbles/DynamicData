// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A type-erased delivery queue that serializes delivery across multiple sources
/// with different item types. Each source gets a typed <see cref="DeliverySubQueue{T}"/>
/// via <see cref="CreateQueue{T}"/>. A single drain loop delivers items from all
/// sub-queues outside the lock, one item per iteration.
/// </summary>
internal sealed class SharedDeliveryQueue
{
    private readonly List<IDrainable> _sources = [];
    private readonly Action? _onDrainComplete;

#if NET9_0_OR_GREATER
    private readonly Lock _gate;
#else
    private readonly object _gate;
#endif

    private int _drainThreadId = -1;
    private volatile bool _isTerminated;
    private bool _hasRemovedQueues;

    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class with its own internal lock.</summary>
    public SharedDeliveryQueue()
        : this(onDrainComplete: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class with its own internal lock
    /// and a callback that fires outside the lock after each drain cycle completes.
    /// </summary>
    public SharedDeliveryQueue(Action? onDrainComplete)
    {
#if NET9_0_OR_GREATER
        _gate = new Lock();
#else
        _gate = new object();
#endif
        _onDrainComplete = onDrainComplete;
    }

#if NET9_0_OR_GREATER
    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class with a caller-provided lock.</summary>
    public SharedDeliveryQueue(Lock gate) => _gate = gate;
#else
    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class with a caller-provided lock.</summary>
    public SharedDeliveryQueue(object gate) => _gate = gate;
#endif

    /// <summary>Gets whether this queue has been terminated.</summary>
    public bool IsTerminated => _isTerminated;

    /// <summary>
    /// Terminates the queue (rejecting further enqueues) and blocks until
    /// any in-flight delivery has completed. After this returns, no more
    /// observer callbacks will fire. Safe to call from within a delivery
    /// callback (skips the spin-wait if the calling thread is the deliverer).
    /// </summary>
    public void EnsureDeliveryComplete()
    {
        using (AcquireReadLock())
        {
            _isTerminated = true;
            foreach (var s in _sources)
            {
                s.Clear();
            }

            if (_drainThreadId == Environment.CurrentManagedThreadId)
                return;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _drainThreadId) != -1)
            spinner.SpinOnce();
    }

    /// <summary>Creates a typed sub-queue bound to the specified observer.</summary>
    public DeliverySubQueue<T> CreateQueue<T>(IObserver<T> observer)
    {
        var queue = new DeliverySubQueue<T>(this, observer);
        EnterLock();
        try
        {
            _sources.Add(queue);
        }
        finally
        {
            ExitLock();
        }

        return queue;
    }

    /// <summary>Acquires the gate for read-only inspection. Does not trigger delivery on dispose.</summary>
    public ReadOnlyScopedAccess AcquireReadLock() => new(this);

    /// <summary>Called by a sub-queue when it is disposed, to trigger lazy compaction.</summary>
    internal void NotifyQueueRemoved() => _hasRemovedQueues = true;

#if NET9_0_OR_GREATER
    internal void EnterLock() => _gate.Enter();

    internal void ExitLock() => _gate.Exit();
#else
    internal void EnterLock() => Monitor.Enter(_gate);

    internal void ExitLock() => Monitor.Exit(_gate);
#endif

    internal void ExitLockAndDrain()
    {
        // Same-thread reentrant: if we're already draining on this thread,
        // deliver newly enqueued items inline. This preserves the same delivery
        // order as Synchronize(lock) — child items emitted synchronously during
        // parent delivery are delivered immediately, not deferred.
        if (_drainThreadId == Environment.CurrentManagedThreadId)
        {
            ExitLock();
            DrainPending();
            return;
        }

        var shouldDrain = false;
        if (_drainThreadId == -1 && !_isTerminated)
        {
            foreach (var s in _sources)
            {
                if (s.HasItems)
                {
                    _drainThreadId = Environment.CurrentManagedThreadId;
                    shouldDrain = true;
                    break;
                }
            }
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
            do
            {
                if (!DrainPending())
                {
                    return; // error terminated the queue
                }

                if (_onDrainComplete is null)
                {
                    break;
                }

                _onDrainComplete();
            }
            while (HasPendingItems());
        }
        finally
        {
            using (AcquireReadLock())
            {
                _drainThreadId = -1;
                CompactRemovedQueues();
            }
        }
    }

    /// <summary>
    /// Delivers all pending items from all sub-queues, one at a time.
    /// Uses <see cref="ReadOnlyScopedAccess"/> (not <c>lock</c>) so it works correctly both
    /// from the outermost drain and from reentrant same-thread calls.
    /// Sub-queues are iterated newest-first (LIFO) so that newer sub-queues
    /// (typically children) are drained before older ones (typically parents).
    /// This ensures pending child items are fully delivered before a parent
    /// delivery can dispose them, which would stop the child's observer and
    /// silently lose any undelivered items.
    /// </summary>
    /// <returns>True if completed normally; false if an error terminated the queue.</returns>
    private bool DrainPending()
    {
        while (true)
        {
            IDrainable? active = null;
            bool isError;

            using (AcquireReadLock())
            {
                for (var i = _sources.Count - 1; i >= 0; i--)
                {
                    if (_sources[i].HasItems)
                    {
                        active = _sources[i];
                        break;
                    }
                }

                if (active is null || _isTerminated)
                {
                    return !_isTerminated;
                }

                isError = active.StageNext();
            }

            // Deliver outside lock
            active.DeliverStaged();

            if (isError)
            {
                using (AcquireReadLock())
                {
                    _isTerminated = true;
                    foreach (var s in _sources)
                    {
                        s.Clear();
                    }
                }

                return false;
            }
        }
    }

    private bool HasPendingItems()
    {
        using var scope = AcquireReadLock();

        foreach (var s in _sources)
        {
            if (s.HasItems)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes dead sub-queues from <see cref="_sources"/>. Must be called
    /// under the lock (inside AcquireReadLock) when no iteration is active.
    /// </summary>
    private void CompactRemovedQueues()
    {
        if (!_hasRemovedQueues)
        {
            return;
        }

        _hasRemovedQueues = false;
        _sources.RemoveAll(s => s.IsRemoved);
    }

    /// <summary>Read-only scoped access. Disposing releases the gate without triggering delivery.</summary>
    public ref struct ReadOnlyScopedAccess
    {
        private SharedDeliveryQueue? _owner;

        internal ReadOnlyScopedAccess(SharedDeliveryQueue owner)
        {
            _owner = owner;
            owner.EnterLock();
        }

        /// <summary>Gets whether any sub-queue has pending items.</summary>
        public readonly bool HasPending
        {
            get
            {
                if (_owner is null)
                {
                    return false;
                }

                if (_owner._drainThreadId != -1)
                {
                    return true;
                }

                foreach (var s in _owner._sources)
                {
                    if (s.HasItems)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Releases the gate lock.</summary>
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

/// <summary>Implemented by typed sub-queues for the drain loop.</summary>
internal interface IDrainable
{
    /// <summary>Gets whether this sub-queue has items.</summary>
    bool HasItems { get; }

    /// <summary>Gets whether this sub-queue has been removed and should be skipped/compacted.</summary>
    bool IsRemoved { get; }

    /// <summary>Dequeues the next item into staging. Returns true if error (terminal).</summary>
    /// <returns>True if the staged item is an error notification.</returns>
    bool StageNext();

    /// <summary>Delivers the staged item to the observer.</summary>
    void DeliverStaged();

    /// <summary>Clears all pending items.</summary>
    void Clear();
}

/// <summary>
/// A typed sub-queue. All enqueue access goes through <see cref="ScopedAccess"/>
/// which acquires the parent's lock.
/// </summary>
internal sealed class DeliverySubQueue<T> : IDrainable, IObserver<T>, IDisposable
{
    private readonly Queue<Notification<T>> _items = new();
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
    bool IDrainable.HasItems => !_isRemoved && _items.Count > 0;

    /// <inheritdoc/>
    bool IDrainable.IsRemoved => _isRemoved;

    /// <summary>Acquires the parent gate. Disposing releases the lock and triggers drain.</summary>
    public ScopedAccess AcquireLock() => new(this);

    /// <summary>Enqueues an OnNext notification via the lock, then drains.</summary>
    public void OnNext(T value)
    {
        using var scope = AcquireLock();
        scope.EnqueueNext(value);
    }

    /// <summary>Enqueues an OnError notification via the lock, then drains.</summary>
    public void OnError(Exception error)
    {
        using var scope = AcquireLock();
        scope.EnqueueError(error);
    }

    /// <summary>Enqueues an OnCompleted notification via the lock, then drains.</summary>
    public void OnCompleted()
    {
        using var scope = AcquireLock();
        scope.EnqueueCompleted();
    }

    /// <summary>
    /// Marks this sub-queue as removed, stopping further enqueues.
    /// Physical removal from the parent's source list happens lazily
    /// during the next drain cycle's completion.
    /// </summary>
    public void Dispose()
    {
        _isRemoved = true;
        _parent.NotifyQueueRemoved();
    }

    /// <inheritdoc/>
    bool IDrainable.StageNext()
    {
        _staged = _items.Dequeue();

        // Errors are fatal to the entire queue and terminate all sub-queues.
        // Completions are scoped to a single sub-queue and delivered normally.
        return _staged.IsError;
    }

    /// <inheritdoc/>
    void IDrainable.DeliverStaged()
    {
        _staged.Accept(_observer);
        _staged = default;
    }

    /// <inheritdoc/>
    void IDrainable.Clear() => _items.Clear();

    private void EnqueueItem(Notification<T> item)
    {
        if (_parent.IsTerminated || _isRemoved)
        {
            return;
        }

        _items.Enqueue(item);
    }

    /// <summary>Scoped access for enqueueing items. Acquires the parent's gate lock.</summary>
    public ref struct ScopedAccess
    {
        private DeliverySubQueue<T>? _owner;

        internal ScopedAccess(DeliverySubQueue<T> owner)
        {
            _owner = owner;
            owner._parent.EnterLock();
        }

        /// <summary>Enqueues an OnNext item.</summary>
        public readonly void EnqueueNext(T item) => _owner?.EnqueueItem(Notification<T>.CreateNext(item));

        /// <summary>Enqueues a terminal error.</summary>
        public readonly void EnqueueError(Exception error) => _owner?.EnqueueItem(Notification<T>.CreateError(error));

        /// <summary>Enqueues a terminal completion.</summary>
        public readonly void EnqueueCompleted() => _owner?.EnqueueItem(Notification<T>.CreateCompleted());

        /// <summary>Releases the parent gate lock and delivers pending items.</summary>
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
