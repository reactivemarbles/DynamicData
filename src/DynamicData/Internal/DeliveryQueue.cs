// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Internal;
#else

namespace DynamicData.Internal;
#endif

/// <summary>
/// A queue that serializes item delivery outside a caller-owned lock.
/// Internally stores <c>Notification&lt;T&gt;</c> values. Delivery
/// is dispatched to an <c>IObserver&lt;T&gt;</c> outside the lock.
/// </summary>
/// <typeparam name="T">The value type delivered via OnNext.</typeparam>
internal sealed class DeliveryQueue<T> : IObserver<T>, IDisposable
    where T : notnull
{
    /// <summary>
    /// The _queue field.
    /// </summary>
    private readonly Queue<Notification<T>> _queue = new(1);

    /// <summary>
    /// The _gate field.
    /// </summary>
    private readonly Lock _gate;

    /// <summary>
    /// The _queueGate field.
    /// </summary>
    private readonly Lock _queueGate = new();

    /// <summary>
    /// The _observer field.
    /// </summary>
    private readonly IObserver<T> _observer;

    /// <summary>
    /// The _activeAccessCount field.
    /// </summary>
    private int _activeAccessCount;

    /// <summary>
    /// The _drainThreadId field.
    /// </summary>
    private int _drainThreadId = -1;

    /// <summary>
    /// The _isTerminated field.
    /// </summary>
    private volatile bool _isTerminated;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class with its own internal lock.
    /// </summary>
    /// <param name="observer">The observer that receives delivered items.</param>
    public DeliveryQueue(IObserver<T> observer)
    {
        _gate = new Lock();
        _observer = observer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryQueue{T}"/> class.
    /// </summary>
    /// <param name="gate">The lock shared with the caller.</param>
    /// <param name="observer">The observer that receives delivered items.</param>
    public DeliveryQueue(Lock gate, IObserver<T> observer)
    {
        _gate = gate;
        _observer = observer;
    }

    /// <summary>
    /// Gets whether this queue has been terminated. Safe to read from any thread.
    /// </summary>
    public bool IsTerminated => _isTerminated;

    /// <summary>
    /// Gets whether the current thread is delivering queued notifications.
    /// </summary>
    internal bool IsDeliveringOnCurrentThread => Volatile.Read(ref _drainThreadId) == Environment.CurrentManagedThreadId;

    /// <summary>
    /// Terminates the queue (rejecting further enqueues) and blocks until
    /// any in-flight delivery has completed. After this returns, no more
    /// observer callbacks will fire. Safe to call from within a delivery
    /// callback (skips the spin-wait if the calling thread is the deliverer).
    /// </summary>
    private void EnsureDeliveryComplete()
    {
        var isDrainThread = false;

        EnterQueueLock();
        try
        {
            _isTerminated = true;
            _queue.Clear();
            isDrainThread = _drainThreadId == Environment.CurrentManagedThreadId;
        }
        finally
        {
            ExitQueueLock();
        }

        // If we're being called from within the drain loop (e.g., downstream
        // disposed during OnNext), the current thread IS the deliverer.
        // The drain loop will see _isTerminated and exit after we return.
        if (isDrainThread)
        {
            return;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _drainThreadId) != -1)
            spinner.SpinOnce();
    }

    /// <inheritdoc/>
    public void Dispose() => EnsureDeliveryComplete();

    /// <summary>
    /// Acquires the gate and returns a scoped access for enqueueing notifications.
    /// Disposing releases the gate and triggers delivery if needed.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ScopedAccess AcquireLock() => new(this);

    /// <summary>
    /// Acquires the gate for read-only inspection. Does not trigger delivery on dispose.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlyScopedAccess AcquireReadLock() => new(this);

    /// <summary>Enqueues an OnNext notification via the lock, then drains.</summary>
    /// <param name="value">The value value.</param>
    public void OnNext(T value)
    {
        using var scope = AcquireLock();
        scope.EnqueueNext(value);
    }

    /// <summary>Enqueues an OnError notification via the lock, then drains.</summary>
    /// <param name="error">The error value.</param>
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

    /// <summary>
    /// Executes the EnterLock operation.
    /// </summary>
    private void EnterLock() => _gate.Enter();

    /// <summary>
    /// Executes the ExitLock operation.
    /// </summary>
    private void ExitLock() => _gate.Exit();
#else

    /// <summary>
    /// Executes the EnterLock operation.
    /// </summary>
    private void EnterLock() => Monitor.Enter(_gate);

    /// <summary>
    /// Executes the ExitLock operation.
    /// </summary>
    private void ExitLock() => Monitor.Exit(_gate);
#endif
#if NET9_0_OR_GREATER

    /// <summary>
    /// Executes the EnterQueueLock operation.
    /// </summary>
    private void EnterQueueLock() => _queueGate.Enter();

    /// <summary>
    /// Executes the ExitQueueLock operation.
    /// </summary>
    private void ExitQueueLock() => _queueGate.Exit();
#else

    /// <summary>
    /// Executes the EnterQueueLock operation.
    /// </summary>
    private void EnterQueueLock() => Monitor.Enter(_queueGate);

    /// <summary>
    /// Executes the ExitQueueLock operation.
    /// </summary>
    private void ExitQueueLock() => Monitor.Exit(_queueGate);
#endif

    /// <summary>
    /// Enters caller-owned access and prevents queue draining until the access is released.
    /// </summary>
    private void EnterAccess()
    {
        Interlocked.Increment(ref _activeAccessCount);

        try
        {
            EnterLock();
        }
        catch
        {
            Interlocked.Decrement(ref _activeAccessCount);
            throw;
        }
    }

    /// <summary>
    /// Releases caller-owned access and starts delivery if this was the final active access.
    /// </summary>
    private void ExitAccessAndDeliver()
    {
        ExitLock();

        if (Interlocked.Decrement(ref _activeAccessCount) == 0)
        {
            DeliverIfNeeded();
        }
    }

    /// <summary>
    /// Executes the EnqueueNotification operation.
    /// </summary>
    /// <param name="item">The item value.</param>
    private void EnqueueNotification(Notification<T> item)
    {
        EnterQueueLock();
        try
        {
            if (_isTerminated)
            {
                return;
            }

            _queue.Enqueue(item);
        }
        finally
        {
            ExitQueueLock();
        }
    }

    /// <summary>
    /// Executes the DeliverIfNeeded operation.
    /// </summary>
    private void DeliverIfNeeded()
    {
        var shouldDeliver = TryStartDelivery();

        if (shouldDeliver)
        {
            DeliverAll();
        }

        bool TryStartDelivery()
        {
            EnterQueueLock();
            try
            {
                if (_drainThreadId != -1 || _queue.Count == 0 || _isTerminated || Volatile.Read(ref _activeAccessCount) != 0)
                {
                    return false;
                }

                Volatile.Write(ref _drainThreadId, Environment.CurrentManagedThreadId);
                return true;
            }
            finally
            {
                ExitQueueLock();
            }
        }

        void DeliverAll()
        {
            try
            {
                while (true)
                {
                    if (!TryTakeNotification(out var notification))
                    {
                        return;
                    }

                    // Deliver outside the lock
                    notification.Accept(_observer);

                    if (notification.IsTerminal)
                    {
                        StopDelivery();
                        return;
                    }
                }
            }
            catch
            {
                StopDelivery();
                throw;
            }
        }

        bool TryTakeNotification(out Notification<T> notification)
        {
            EnterQueueLock();
            try
            {
                if (_queue.Count == 0 || _isTerminated || Volatile.Read(ref _activeAccessCount) != 0)
                {
                    Volatile.Write(ref _drainThreadId, -1);
                    notification = default;
                    return false;
                }

                notification = _queue.Dequeue();

                // Mark terminated BEFORE delivery so concurrent code
                // (e.g., InvokePreview) sees the terminal state immediately.
                if (notification.IsTerminal)
                {
                    _isTerminated = true;
                    _queue.Clear();
                }

                return true;
            }
            finally
            {
                ExitQueueLock();
            }
        }

        void StopDelivery()
        {
            EnterQueueLock();
            try
            {
                Volatile.Write(ref _drainThreadId, -1);
            }
            finally
            {
                ExitQueueLock();
            }
        }
    }

    /// <summary>
    /// Gets whether queue delivery is currently pending or in progress.
    /// </summary>
    /// <returns><see langword="true"/> when there are queued or in-flight notifications.</returns>
    private bool HasPendingNotifications()
    {
        EnterQueueLock();
        try
        {
            return _queue.Count > 0 || _drainThreadId != -1;
        }
        finally
        {
            ExitQueueLock();
        }
    }

/// <summary>
/// Scoped access for enqueueing notifications under the gate lock.
/// </summary>
public ref struct ScopedAccess
    {
        /// <summary>
        /// The _owner field.
        /// </summary>
        private DeliveryQueue<T>? _owner;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopedAccess"/> struct.
        /// </summary>
        /// <param name="owner">The owner value.</param>
        internal ScopedAccess(DeliveryQueue<T> owner)
        {
            _owner = owner;
            owner.EnterAccess();
        }

        /// <summary>Enqueues an OnNext notification.</summary>
        /// <param name="value">The value value.</param>
        public readonly void EnqueueNext(T value) => _owner?.EnqueueNotification(Notification<T>.CreateNext(value));

        /// <summary>Enqueues an OnError notification (terminal).</summary>
        /// <param name="error">The error value.</param>
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
            owner.ExitAccessAndDeliver();
        }
    }

/// <summary>
/// Read-only scoped access. Disposing releases the gate and resumes any deferred delivery.
/// </summary>
public ref struct ReadOnlyScopedAccess
    {
        /// <summary>
        /// The _owner field.
        /// </summary>
        private DeliveryQueue<T>? _owner;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyScopedAccess"/> struct.
        /// </summary>
        /// <param name="owner">The owner value.</param>
        internal ReadOnlyScopedAccess(DeliveryQueue<T> owner)
        {
            _owner = owner;
            owner.EnterAccess();
        }

        /// <summary>Gets whether there are notifications pending delivery.</summary>
        public readonly bool HasPending => _owner?.HasPendingNotifications() == true;

        /// <summary>Releases the gate lock.</summary>
        public void Dispose()
        {
            var owner = _owner;
            if (owner is null)
            {
                return;
            }

            _owner = null;
            owner.ExitAccessAndDeliver();
        }
    }
}
