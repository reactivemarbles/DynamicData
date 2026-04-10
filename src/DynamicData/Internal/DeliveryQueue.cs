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
internal sealed class DeliveryQueue<T>
{
    private readonly Queue<Notification<T>> _queue = new();

#if NET9_0_OR_GREATER
    private readonly Lock _gate;
#else
    private readonly object _gate;
#endif

    private IObserver<T>? _observer;
    private bool _isDelivering;
    private volatile bool _isTerminated;

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
    /// <summary>Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class without an observer. Call <see cref="SetObserver"/> before items are drained.</summary>
    public DeliveryQueue(Lock gate) => _gate = gate;
#else
    /// <summary>Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class without an observer. Call <see cref="SetObserver"/> before items are drained.</summary>
    public DeliveryQueue(object gate) => _gate = gate;
#endif

    /// <summary>
    /// Sets the delivery observer. Must be called exactly once, before any items are drained.
    /// </summary>
    internal void SetObserver(IObserver<T> observer) =>
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));

    /// <summary>
    /// Gets whether this queue has been terminated. Safe to read from any thread.
    /// </summary>
    public bool IsTerminated => _isTerminated;

    /// <summary>
    /// Acquires the gate and returns a scoped access for enqueueing notifications.
    /// Disposing releases the gate and triggers delivery if needed.
    /// </summary>
    public ScopedAccess AcquireLock() => new(this);

    /// <summary>
    /// Acquires the gate for read-only inspection. Does not trigger delivery on dispose.
    /// </summary>
    public ReadOnlyScopedAccess AcquireReadLock() => new(this);

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
            if (_isDelivering || _queue.Count == 0)
            {
                return false;
            }

            _isDelivering = true;
            return true;
        }

        void DeliverAll()
        {
            try
            {
                while (true)
                {
                    Notification<T> notification;

                    lock (_gate)
                    {
                        if (_queue.Count == 0)
                        {
                            _isDelivering = false;
                            return;
                        }

                        notification = _queue.Dequeue();
                    }

                    // Deliver outside the lock
                    notification.Accept(_observer!);

                    if (notification.IsTerminal)
                    {
                        lock (_gate)
                        {
                            _isTerminated = true;
                            _isDelivering = false;
                            _queue.Clear();
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
        public readonly void Enqueue(T value) => _owner?.EnqueueNotification(Notification<T>.Next(value));

        /// <summary>Enqueues an OnError notification (terminal).</summary>
        public readonly void EnqueueError(Exception error) => _owner?.EnqueueNotification(Notification<T>.OnError(error));

        /// <summary>Enqueues an OnCompleted notification (terminal).</summary>
        public readonly void EnqueueCompleted() => _owner?.EnqueueNotification(Notification<T>.Completed);

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
            _owner is not null && (_owner._queue.Count > 0 || _owner._isDelivering);

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