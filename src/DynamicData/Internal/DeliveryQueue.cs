// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Internal;

/// <summary>
/// A queue that serializes item delivery outside a caller-owned lock.
/// Use <see cref="AcquireLock"/> to obtain a scoped ScopedAccess for enqueueing items.
/// When the ScopedAccess is disposed, the lock is released
/// and pending items are delivered. Only one thread delivers at a time.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
internal sealed class DeliveryQueue<TItem>
{
    private readonly Queue<TItem> _queue = new();
    private readonly Func<TItem, bool> _deliver;

#if NET9_0_OR_GREATER
    private readonly Lock _gate;
#else
    private readonly object _gate;
#endif

    private bool _isDelivering;
    private volatile bool _isTerminated;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryQueue{TItem}"/> class.
    /// </summary>
    /// <param name="gate">The lock shared with the caller. The queue acquires this
    /// lock during <see cref="AcquireLock"/> and during the dequeue step of delivery.</param>
    /// <param name="deliver">Callback invoked for each item, outside the lock. Returns false if the item was terminal, which stops further delivery.</param>
#if NET9_0_OR_GREATER
    public DeliveryQueue(Lock gate, Func<TItem, bool> deliver)
#else
    public DeliveryQueue(object gate, Func<TItem, bool> deliver)
#endif
    {
        _gate = gate;
        _deliver = deliver;
    }

    /// <summary>
    /// Gets whether this queue has been terminated. Safe to read from any thread.
    /// </summary>
    public bool IsTerminated => _isTerminated;

    /// <summary>
    /// Acquires the gate and returns a scoped ScopedAccess for enqueueing items.
    /// When the ScopedAccess is disposed, the gate is released
    /// and delivery runs if needed. The ScopedAccess is a ref struct and cannot
    /// escape the calling method.
    /// </summary>
    public ScopedAccess AcquireLock() => new(this);

#if NET9_0_OR_GREATER
    private void EnterLock() => _gate.Enter();

    private void ExitLock() => _gate.Exit();
#else
    private void EnterLock() => Monitor.Enter(_gate);

    private void ExitLock() => Monitor.Exit(_gate);
#endif

    private void EnqueueItem(TItem item)
    {
        if (_isTerminated)
        {
            return;
        }

        _queue.Enqueue(item);
    }

    private void ExitLockAndDeliver()
    {
        // Before releasing the lock, check if we should start delivery. Only one thread can succeed
        var shouldDeliver = TryStartDelivery();

        // Now release the lock. We do this before delivering to allow other threads to enqueue items while delivery is in progress.
        ExitLock();

        // If this thread has been chosen to deliver, do it now that the lock is released.
        // If not, another thread is already delivering or there are no items to deliver.
        if (shouldDeliver)
        {
            DeliverAll();
        }

        bool TryStartDelivery()
        {
            // Bail if something is already delivering or there's nothing to do
            if (_isDelivering || _queue.Count == 0)
            {
                return false;
            }

            // Mark that we're doing the delivering
            _isDelivering = true;
            return true;
        }

        void DeliverAll()
        {
            try
            {
                while (true)
                {
                    TItem item;

                    // Inside of the lock, see if there is work and get the next item to deliver.
                    // If there is no work, mark that we're done delivering and exit.
                    lock (_gate)
                    {
                        if (_queue.Count == 0)
                        {
                            _isDelivering = false;
                            return;
                        }

                        item = _queue.Dequeue();
                    }

                    // Outside of the lock, invoke the callback to deliver the item.
                    // If delivery returns false, it means the item was terminal
                    // and we should stop delivering and clear the queue.
                    if (!_deliver(item))
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
                // Safety net: if an exception bypassed the normal exit paths,
                // ensure _isDelivering is reset so the queue doesn't get stuck.
                lock (_gate)
                {
                    _isDelivering = false;
                }

                throw;
            }
        }
    }

    /// <summary>
    /// A scoped ScopedAccess for working under the gate lock. All queue mutation
    /// goes through this ScopedAccess, ensuring the lock is held. Disposing
    /// releases the lock and triggers delivery if needed.
    /// </summary>
    public ref struct ScopedAccess
    {
        private DeliveryQueue<TItem>? _owner;

        internal ScopedAccess(DeliveryQueue<TItem> owner)
        {
            _owner = owner;
            owner.EnterLock();
        }

        /// <summary>
        /// Adds an item to the queue. Ignored if the queue has been terminated.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        public readonly void Enqueue(TItem item) => _owner?.EnqueueItem(item);

        /// <summary>
        /// Releases the gate lock and delivers pending items if this thread
        /// holds the delivery token.
        /// </summary>
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
}
