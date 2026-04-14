// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A queue that serializes item delivery outside a caller-owned lock.
/// Internally stores <see cref="Notification{T}"/> values. Delivery
/// is dispatched to an <see cref="IObserver{T}"/> outside the lock.
/// </summary>
/// <typeparam name="T">The value type delivered via OnNext.</typeparam>
internal sealed class DeliveryQueue<T> : IObserver<T>
    where T : notnull
{
    private readonly Queue<Notification<T>> _queue = new();
    private readonly List<Notification<T>> _drainBuffer = new();

#if NET9_0_OR_GREATER
    private readonly Lock _gate;
#else
    private readonly object _gate;
#endif

    private IObserver<T>? _observer;
    private int _drainThreadId = -1;
    private volatile bool _isTerminated;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class with its own internal lock.
    /// </summary>
    /// <param name="observer">The observer that receives delivered items.</param>
    public DeliveryQueue(IObserver<T> observer)
    {
#if NET9_0_OR_GREATER
        _gate = new Lock();
#else
        _gate = new object();
#endif
        _observer = observer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class.
    /// </summary>
    /// <param name="gate">The lock shared with the caller.</param>
    /// <param name="observer">The observer that receives delivered items.</param>
#if NET9_0_OR_GREATER
    public DeliveryQueue(Lock gate, IObserver<T> observer)
#else
    public DeliveryQueue(object gate, IObserver<T> observer)
#endif
    {
        _gate = gate;
        _observer = observer;
    }

#if NET9_0_OR_GREATER
    /// <summary>Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class with a deferred observer. Call <see cref="SetObserver"/> before items are drained.</summary>
    internal DeliveryQueue(Lock gate) => _gate = gate;
#else
    /// <summary>Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class with a deferred observer. Call <see cref="SetObserver"/> before items are drained.</summary>
    internal DeliveryQueue(object gate) => _gate = gate;
#endif

    /// <summary>Sets the delivery observer. Must be called exactly once, before any items are drained.</summary>
    internal void SetObserver(IObserver<T> observer)
    {
        if (_observer is not null)
            throw new InvalidOperationException("Observer has already been set.");

        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
    }

    /// <summary>
    /// Gets whether this queue has been terminated. Safe to read from any thread.
    /// </summary>
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
            _queue.Clear();

            // If we're being called from within the drain loop (e.g., downstream
            // disposed during OnNext), the current thread IS the deliverer.
            // The drain loop will see _isTerminated and exit after we return.
            if (_drainThreadId == Environment.CurrentManagedThreadId)
                return;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _drainThreadId) != -1)
            spinner.SpinOnce();
    }

    /// <summary>
    /// Acquires the gate and returns a scoped access for enqueueing notifications.
    /// Disposing releases the gate and triggers delivery if needed.
    /// </summary>
    public ScopedAccess AcquireLock() => new(this);

    /// <summary>
    /// Acquires the gate for read-only inspection. Does not trigger delivery on dispose.
    /// </summary>
    public ReadOnlyScopedAccess AcquireReadLock() => new(this);

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

#if NET9_0_OR_GREATER
    private void EnterLock() => _gate.Enter();

    private void ExitLock() => _gate.Exit();
#else
    private void EnterLock() => Monitor.Enter(_gate);

    private void ExitLock() => Monitor.Exit(_gate);
#endif

    private void EnqueueNotification(Notification<T> item)
    {
        if (_isTerminated)
        {
            return;
        }

        _queue.Enqueue(item);
    }

    private void ExitLockAndDeliver()
    {
        var shouldDeliver = TryStartDelivery();
        ExitLock();

        if (shouldDeliver)
        {
            DeliverAll();
        }

        bool TryStartDelivery()
        {
            if (_drainThreadId != -1 || _queue.Count == 0)
            {
                return false;
            }

            _drainThreadId = Environment.CurrentManagedThreadId;
            return true;
        }

        void DeliverAll()
        {
            try
            {
                while (true)
                {
                    bool hasTerminal;

                    // Batch: dequeue all pending items in one lock acquisition.
                    // Under contention, multiple producers can enqueue while we deliver.
                    // Batching reduces lock acquisitions from N to 1 per drain cycle.
                    using (AcquireReadLock())
                    {
                        if (_queue.Count == 0 || _isTerminated)
                        {
                            _drainThreadId = -1;
                            return;
                        }

                        hasTerminal = false;
                        while (_queue.Count > 0)
                        {
                            var item = _queue.Dequeue();
                            _drainBuffer.Add(item);
                            if (item.IsTerminal)
                            {
                                _isTerminated = true;
                                _queue.Clear();
                                hasTerminal = true;
                                break;
                            }
                        }
                    }

                    // Deliver batch outside the lock. Track index so we can
                    // re-enqueue undelivered items if the observer throws.
                    var deliveredCount = 0;
                    try
                    {
                        for (var i = 0; i < _drainBuffer.Count; i++)
                        {
                            _drainBuffer[i].Accept(_observer!);
                            deliveredCount = i + 1;
                        }
                    }
                    catch
                    {
                        // Skip the failed item (deliveredCount), preserve items after it.
                        var remainderStart = deliveredCount + 1;
                        if (remainderStart < _drainBuffer.Count)
                        {
                            using (AcquireReadLock())
                            {
                                var existing = _queue.Count;
                                for (var i = remainderStart; i < _drainBuffer.Count; i++)
                                {
                                    _queue.Enqueue(_drainBuffer[i]);
                                }

                                // Rotate existing items to maintain order.
                                for (var i = 0; i < existing; i++)
                                {
                                    _queue.Enqueue(_queue.Dequeue());
                                }
                            }
                        }

                        _drainBuffer.Clear();

                        using (AcquireReadLock())
                        {
                            _drainThreadId = -1;
                        }

                        throw;
                    }

                    _drainBuffer.Clear();

                    if (hasTerminal)
                    {
                        using (AcquireReadLock())
                        {
                            _drainThreadId = -1;
                        }

                        return;
                    }
                }
            }
            catch
            {
                _drainBuffer.Clear();

                using (AcquireReadLock())
                {
                    _drainThreadId = -1;
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Scoped access for enqueueing notifications under the gate lock.
    /// </summary>
    public ref struct ScopedAccess
    {
        private DeliveryQueue<T>? _owner;

        internal ScopedAccess(DeliveryQueue<T> owner)
        {
            _owner = owner;
            owner.EnterLock();
        }

        /// <summary>Enqueues an OnNext notification.</summary>
        public readonly void EnqueueNext(T value) => _owner?.EnqueueNotification(Notification<T>.CreateNext(value));

        /// <summary>Enqueues an OnError notification (terminal).</summary>
        public readonly void EnqueueError(Exception error) => _owner?.EnqueueNotification(Notification<T>.CreateError(error));

        /// <summary>Enqueues an OnCompleted notification (terminal).</summary>
        public readonly void EnqueueCompleted() => _owner?.EnqueueNotification(Notification<T>.CreateCompleted());

        /// <summary>Releases the gate lock and delivers pending items.</summary>
        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
            {
                return;
            }

            _owner = null;
            owner.ExitLockAndDeliver();
        }
    }

    /// <summary>
    /// Read-only scoped access. Disposing releases the gate without triggering delivery.
    /// </summary>
    public ref struct ReadOnlyScopedAccess
    {
        private DeliveryQueue<T>? _owner;

        internal ReadOnlyScopedAccess(DeliveryQueue<T> owner)
        {
            _owner = owner;
            owner.EnterLock();
        }

        /// <summary>Gets whether there are notifications pending delivery.</summary>
        public readonly bool HasPending =>
            _owner is not null && (_owner._queue.Count > 0 || _owner._drainThreadId != -1);

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
