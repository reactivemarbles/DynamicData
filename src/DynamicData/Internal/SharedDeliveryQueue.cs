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

#if NET9_0_OR_GREATER
    private readonly Lock _gate;
#else
    private readonly object _gate;
#endif

    private volatile bool _isDelivering;
    private int _drainThreadId = -1;
    private volatile bool _isTerminated;

#if NET9_0_OR_GREATER
    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class.</summary>
    public SharedDeliveryQueue(Lock gate) => _gate = gate;
#else
    /// <summary>Initializes a new instance of the <see cref="SharedDeliveryQueue"/> class.</summary>
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
        lock (_gate)
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
        while (_isDelivering)
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

#if NET9_0_OR_GREATER
    internal void EnterLock() => _gate.Enter();

    internal void ExitLock() => _gate.Exit();
#else
    internal void EnterLock() => Monitor.Enter(_gate);

    internal void ExitLock() => Monitor.Exit(_gate);
#endif

    internal void ExitLockAndDrain()
    {
        var shouldDrain = false;
        if (!_isDelivering && !_isTerminated)
        {
            foreach (var s in _sources)
            {
                if (s.HasItems)
                {
                    _isDelivering = true;
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
            while (true)
            {
                IDrainable? active = null;
                var isError = false;

                lock (_gate)
                {
                    foreach (var s in _sources)
                    {
                        if (s.HasItems)
                        {
                            active = s;
                            break;
                        }
                    }

                    if (active is null)
                    {
                        _isDelivering = false;
                        return;
                    }

                    isError = active.StageNext();
                }

                // Deliver outside lock
                active.DeliverStaged();

                // Errors terminate the entire queue AFTER delivery
                if (isError)
                {
                    lock (_gate)
                    {
                        _isTerminated = true;
                        _isDelivering = false;
                        foreach (var s in _sources)
                        {
                            s.Clear();
                        }
                    }

                    return;
                }
            }
        }
        catch
        {
            lock (_gate)
            {
                _isDelivering = false;
            }

            throw;
        }
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

                if (_owner._isDelivering)
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
internal sealed class DeliverySubQueue<T> : IDrainable
{
    private readonly Queue<Notification<T>> _items = new();
    private readonly SharedDeliveryQueue _parent;
    private readonly IObserver<T> _observer;
    private Notification<T> _staged;

    internal DeliverySubQueue(SharedDeliveryQueue parent, IObserver<T> observer)
    {
        _parent = parent;
        _observer = observer;
    }

    /// <inheritdoc/>
    public bool HasItems => _items.Count > 0;

    /// <summary>Acquires the parent gate. Disposing releases the lock and triggers drain.</summary>
    public ScopedAccess AcquireLock() => new(this);

    /// <inheritdoc/>
    public bool StageNext()
    {
        _staged = _items.Dequeue();
        return _staged.Error is not null;
    }

    /// <inheritdoc/>
    public void DeliverStaged() => _staged.Accept(_observer);

    /// <inheritdoc/>
    public void Clear() => _items.Clear();

    private void EnqueueItem(Notification<T> item)
    {
        if (_parent.IsTerminated)
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
        public readonly void Enqueue(T item) => _owner?.EnqueueItem(Notification<T>.Next(item));

        /// <summary>Enqueues a terminal error.</summary>
        public readonly void EnqueueError(Exception error) => _owner?.EnqueueItem(Notification<T>.OnError(error));

        /// <summary>Enqueues a terminal completion.</summary>
        public readonly void EnqueueCompleted() => _owner?.EnqueueItem(Notification<T>.Completed);

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
