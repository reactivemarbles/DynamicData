// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace DynamicData.Internal;

/// <summary>
/// A type-erased delivery queue that serializes delivery across multiple sources
/// with different item types. Each source gets a typed <see cref="DeliverySubQueue{T}"/>
/// via <see cref="CreateQueue{T}"/>. A single drain loop delivers items from all
/// sub-queues outside the lock, one item per iteration. An <see cref="Bitset"/>
/// tracks which sub-queues have pending items, replacing O(N) scans with O(1) lookups.
/// </summary>
internal sealed class SharedDeliveryQueue : IDisposable
{
    private readonly List<DrainableBase> _sources = [];
    private readonly Action? _onDrainComplete;

#if NET9_0_OR_GREATER
    private readonly Lock _gate;
#else
    private readonly object _gate;
#endif

    private Bitset _activeBits = new();
    private int _deadCount;
    private int _drainThreadId = -1;
    private volatile bool _isTerminated;

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
    public bool IsTerminated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isTerminated;
    }

    /// <summary>
    /// Terminates the queue (rejecting further enqueues) and blocks until
    /// any in-flight delivery has completed. After this returns, no more
    /// observer callbacks will fire. Safe to call from within a delivery
    /// callback (skips the spin-wait if the calling thread is the deliverer).
    /// </summary>
    private void EnsureDeliveryComplete()
    {
        EnterLock();

        _isTerminated = true;
        _activeBits.ClearAll();

        foreach (var s in _sources)
        {
            s.Clear();
        }

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

    /// <summary>Disposes the queue by calling <see cref="EnsureDeliveryComplete"/>.</summary>
    public void Dispose() => EnsureDeliveryComplete();

    /// <summary>Creates a typed sub-queue bound to the specified observer.</summary>
    public DeliverySubQueue<T> CreateQueue<T>(IObserver<T> observer)
    {
        EnterLock();
        try
        {
            var index = _sources.Count;
            var queue = new DeliverySubQueue<T>(this, observer, index);
            _sources.Add(queue);

            return queue;
        }
        finally
        {
            ExitLock();
        }
    }

    /// <summary>Acquires the gate for read-only inspection. Does not trigger delivery on dispose.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyScopedAccess AcquireReadLock() => new(this);

    /// <summary>Called by a sub-queue when it is disposed. Clears its active bit and tracks dead slots.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void NotifyQueueRemoved(int index)
    {
        _activeBits.Clear(index);
        _deadCount++;
    }

    /// <summary>Sets the active bit for a sub-queue when an item is enqueued.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetActive(int index) => _activeBits.Set(index);

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

        // Same-thread reentrant: if we're already draining on this thread,
        // deliver newly enqueued items inline. This preserves the same delivery
        // order as Synchronize(lock): child items emitted synchronously during
        // parent delivery are delivered immediately, not deferred.
        if (_drainThreadId == currentThreadId)
        {
            ExitLock();
            DrainPending();
            return;
        }

        var shouldDrain = false;
        if (_drainThreadId == -1 && !_isTerminated && _activeBits.HasAny())
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
                    EnterLock();
                    try
                    {
                        _drainThreadId = -1;
                        CompactIfNeeded();
                    }
                    finally
                    {
                        ExitLock();
                    }

                    return;
                }

                if (_onDrainComplete is not null)
                {
                    _onDrainComplete();
                }

                // Atomically check for pending items and release drain ownership
                // if empty. This closes the TOCTOU window: if we checked and released
                // in separate lock scopes, Thread B could enqueue between them,
                // see _drainThreadId != -1, and rely on us to drain, but we'd exit
                // without draining Thread B's item.
                EnterLock();

                if (_activeBits.HasAny() && !_isTerminated)
                {
                    // Items arrived during _onDrainComplete. Loop back to drain them.
                    ExitLock();
                    continue;
                }

                try
                {
                    _drainThreadId = -1;
                    CompactIfNeeded();
                }
                finally
                {
                    ExitLock();
                }

                return;
            }
        }
        catch
        {
            EnterLock();
            _drainThreadId = -1;
            ExitLock();
            throw;
        }
    }

    /// <summary>
    /// Delivers all pending items from all sub-queues, one at a time.
    /// Sub-queues are found via the active bitset using LZCNT (highest-index first
    /// for LIFO ordering). When one sub-queue's delivery can dispose another
    /// (parent disposing a child), the child must drain first to prevent pending
    /// child notifications from being silently lost. Newer sub-queues are always
    /// children of older ones, so LIFO provides this guarantee.
    /// </summary>
    /// <returns>True if completed normally; false if an error terminated the queue.</returns>
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

            var sourceIndex = _activeBits.FindHighest();
            if (sourceIndex < 0)
            {
                ExitLock();
                return true;
            }

            var active = _sources[sourceIndex];
            var isError = active.StageNext();

            // If sub-queue is now empty, clear its active bit immediately.
            if (!active.HasItems)
            {
                _activeBits.Clear(sourceIndex);
            }

            ExitLock();

            active.DeliverStaged();

            if (isError)
            {
                EnterLock();
                _isTerminated = true;
                _activeBits.ClearAll();
                foreach (var s in _sources)
                {
                    s.Clear();
                }

                ExitLock();
                return false;
            }
        }
    }

    /// <summary>
    /// Compacts the source list when dead slots exceed 50% of capacity.
    /// Rebuilds indices and the bitset atomically. Must be called under lock.
    /// </summary>
    private void CompactIfNeeded()
    {
        if (_deadCount == 0 || _deadCount <= _sources.Count / 2)
        {
            return;
        }

        _deadCount = 0;
        _activeBits.ClearAll();

        var writeIndex = 0;
        for (var readIndex = 0; readIndex < _sources.Count; readIndex++)
        {
            var source = _sources[readIndex];
            if (!source.IsRemoved)
            {
                source.Index = writeIndex;
                _sources[writeIndex] = source;

                if (source.HasItems)
                {
                    SetActive(writeIndex);
                }

                writeIndex++;
            }
        }

        // Remove trailing dead entries
        if (writeIndex < _sources.Count)
        {
            _sources.RemoveRange(writeIndex, _sources.Count - writeIndex);
        }

        _activeBits.Compact();
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

        /// <summary>Gets whether any sub-queue has pending items.</summary>
        public readonly bool HasPending
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_owner is null)
                {
                    return false;
                }

                return _owner._drainThreadId != -1 || _owner._activeBits.HasAny();
            }
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

/// <summary>Base class for typed sub-queues. Enables devirtualization in the drain loop.</summary>
internal abstract class DrainableBase
{
    /// <summary>Gets whether this sub-queue has items.</summary>
    internal abstract bool HasItems { get; }

    /// <summary>Gets whether this sub-queue has been removed and should be skipped/compacted.</summary>
    internal abstract bool IsRemoved { get; }

    /// <summary>Gets or sets the stable index in the parent's source list.</summary>
    internal abstract int Index { get; set; }

    /// <summary>Dequeues the next item into staging. Returns true if error (terminal).</summary>
    /// <returns>True if the staged item is an error notification.</returns>
    internal abstract bool StageNext();

    /// <summary>Delivers the staged item to the observer.</summary>
    internal abstract void DeliverStaged();

    /// <summary>Clears all pending items.</summary>
    internal abstract void Clear();
}

/// <summary>
/// A typed sub-queue. All enqueue access goes through <see cref="ScopedAccess"/>
/// which acquires the parent's lock.
/// </summary>
internal sealed class DeliverySubQueue<T> : DrainableBase, IObserver<T>, IDisposable
{
    private readonly Queue<Notification<T>> _items = new(1);
    private readonly SharedDeliveryQueue _parent;
    private readonly IObserver<T> _observer;
    private Notification<T> _staged;
    private int _index;
    private bool _isRemoved;

    internal DeliverySubQueue(SharedDeliveryQueue parent, IObserver<T> observer, int index)
    {
        _parent = parent;
        _observer = observer;
        _index = index;
    }

    /// <inheritdoc/>
    internal override bool HasItems
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !_isRemoved && _items.Count > 0;
    }

    /// <inheritdoc/>
    internal override bool IsRemoved
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _isRemoved;
    }

    /// <inheritdoc/>
    internal override int Index
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _index = value;
    }

    /// <summary>Acquires the parent gate. Disposing releases the lock and triggers drain.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Marks this sub-queue as removed under the parent lock, clearing pending items
    /// and notifying the parent for GC compaction. Idempotent.
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
            _parent.NotifyQueueRemoved(_index);
        }
        finally
        {
            _parent.ExitLock();
        }
    }

    /// <inheritdoc/>
    internal override bool StageNext()
    {
        _staged = _items.Dequeue();
        return _staged.IsError;
    }

    /// <inheritdoc/>
    internal override void DeliverStaged()
    {
        _staged.Accept(_observer);
        _staged = default;
    }

    /// <inheritdoc/>
    internal override void Clear() => _items.Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnqueueItem(Notification<T> item)
    {
        if (_parent.IsTerminated || _isRemoved)
        {
            return;
        }

        _items.Enqueue(item);
        _parent.SetActive(_index);
    }

    /// <summary>Scoped access for enqueueing items. Acquires the parent's gate lock.</summary>
    public ref struct ScopedAccess
    {
        private DeliverySubQueue<T>? _owner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ScopedAccess(DeliverySubQueue<T> owner)
        {
            _owner = owner;
            owner._parent.EnterLock();
        }

        /// <summary>Enqueues an OnNext item.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void EnqueueNext(T item) => _owner?.EnqueueItem(Notification<T>.CreateNext(item));

        /// <summary>Enqueues a terminal error.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void EnqueueError(Exception error) => _owner?.EnqueueItem(Notification<T>.CreateError(error));

        /// <summary>Enqueues a terminal completion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
