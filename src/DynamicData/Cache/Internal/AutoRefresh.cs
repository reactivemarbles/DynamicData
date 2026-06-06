// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Cache.Internal;

internal sealed class AutoRefresh<TObject, TKey, TAny>(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TKey, IObservable<TAny>> reEvaluator,
        TimeSpan? buffer = null,
        IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
    {
        var orchestrator = buffer is { } window
            ? (CacheChangeHandlerBase<TObject, TKey, Change<TObject, TKey>, IChangeSet<TObject, TKey>>)new BufferedOrchestrator(reEvaluator, window, scheduler ?? GlobalConfig.DefaultScheduler)
            : new UnbufferedOrchestrator(reEvaluator);
        return source.OrchestrateMany(orchestrator).SubscribeSafe(observer);
    });

    /// <summary>
    /// Unbuffered AutoRefresh: forwards source changes verbatim and emits each refresh notification
    /// individually as a single-change <see cref="ChangeSet{TObject, TKey}"/>. Refreshes for keys the
    /// source has touched within the same drain cycle are suppressed (fixes #1099: the sync
    /// Add+Refresh scenario where a reevaluator fires synchronously during item subscription would
    /// otherwise emit both Add and Refresh for the same key).
    /// </summary>
    private sealed class UnbufferedOrchestrator(Func<TObject, TKey, IObservable<TAny>> reEvaluator)
        : CacheChangeHandlerBase<TObject, TKey, Change<TObject, TKey>, IChangeSet<TObject, TKey>>
    {
        private readonly HashSet<TKey> _sourceTouched = [];
        private IChangeSet<TObject, TKey>? _pendingSource;
        private List<Change<TObject, TKey>>? _pendingRefreshes;

        public override void OnInner(Change<TObject, TKey> refresh, TKey key)
        {
            if (_sourceTouched.Contains(key))
            {
                return;
            }

            (_pendingRefreshes ??= []).Add(refresh);
        }

        public override void Emit(IObserver<IChangeSet<TObject, TKey>> observer)
        {
            _sourceTouched.Clear();

            var pending = _pendingSource;
            _pendingSource = null;
            if (pending is { Count: > 0 })
            {
                observer.OnNext(pending);
            }

            var refreshes = _pendingRefreshes;
            _pendingRefreshes = null;
            if (refreshes is { Count: > 0 })
            {
                observer.OnNext(new ChangeSet<TObject, TKey>(refreshes));
            }
        }

        protected override void OnItemAdded(TObject item, TKey key)
        {
            _sourceTouched.Add(key);
            Context.Track(key, reEvaluator(item, key).Select(_ => new Change<TObject, TKey>(ChangeReason.Refresh, key, item)));
        }

        protected override void OnItemUpdated(TObject current, TObject previous, TKey key) => OnItemAdded(current, key);

        protected override void OnItemRemoved(TObject item, TKey key)
        {
            _sourceTouched.Add(key);
            Context.Track(key, null);
        }

        protected override void OnItemRefreshed(TObject item, TKey key) => _sourceTouched.Add(key);

        protected override void OnChangeSetProcessed(IChangeSet<TObject, TKey> changes) =>
            _pendingSource = _pendingSource is null
                ? changes
                : new ChangeSet<TObject, TKey>(_pendingSource.Concat(changes));
    }

    /// <summary>
    /// Buffered AutoRefresh: source changes flow through immediately; refreshes are collected into a
    /// time-bounded buffer and emitted as a single <see cref="ChangeSet{TObject, TKey}"/> per window,
    /// deduplicated by key within the window. The buffered stream is routed through
    /// <see cref="ICacheOrchestratorContext{TKey, TInner}.Serialize"/> so the per-window flush runs
    /// under the same serialization gate as source and inner events.
    /// </summary>
    private sealed class BufferedOrchestrator : CacheChangeHandlerBase<TObject, TKey, Change<TObject, TKey>, IChangeSet<TObject, TKey>>, IDisposable
    {
        private readonly Subject<Change<TObject, TKey>> _refreshes = new();
        private readonly HashSet<TKey> _dedupBuffer = [];
        private readonly Func<TObject, TKey, IObservable<TAny>> _reEvaluator;
        private readonly TimeSpan _window;
        private readonly IScheduler _scheduler;
        private IChangeSet<TObject, TKey>? _pendingSource;
        private List<Change<TObject, TKey>>? _bufferedRefreshes;
        private IDisposable? _bufferSubscription;
        private bool _shouldFlushBuffered;

        public BufferedOrchestrator(Func<TObject, TKey, IObservable<TAny>> reEvaluator, TimeSpan window, IScheduler scheduler)
        {
            _reEvaluator = reEvaluator;
            _window = window;
            _scheduler = scheduler;
        }

        public void Dispose()
        {
            _bufferSubscription?.Dispose();
            _refreshes.Dispose();
        }

        public override void OnInner(Change<TObject, TKey> refresh, TKey key)
        {
            _bufferSubscription ??= Context.Serialize(_refreshes.Buffer(_window, _scheduler)).Subscribe(OnRefreshBatch);
            _refreshes.OnNext(refresh);
        }

        public override void Emit(IObserver<IChangeSet<TObject, TKey>> observer)
        {
            var pending = _pendingSource;
            _pendingSource = null;
            if (pending is { Count: > 0 })
            {
                observer.OnNext(pending);
            }

            if (!_shouldFlushBuffered)
            {
                return;
            }

            _shouldFlushBuffered = false;
            var refreshes = _bufferedRefreshes;
            _bufferedRefreshes = null;
            if (refreshes is { Count: > 0 })
            {
                observer.OnNext(new ChangeSet<TObject, TKey>(refreshes));
            }
        }

        protected override void OnItemAdded(TObject item, TKey key) =>
            Context.Track(key, _reEvaluator(item, key).Select(_ => new Change<TObject, TKey>(ChangeReason.Refresh, key, item)));

        protected override void OnItemRemoved(TObject item, TKey key) => Context.Track(key, null);

        protected override void OnChangeSetProcessed(IChangeSet<TObject, TKey> changes) =>
            _pendingSource = _pendingSource is null
                ? changes
                : new ChangeSet<TObject, TKey>(_pendingSource.Concat(changes));

        private void OnRefreshBatch(IList<Change<TObject, TKey>> batch)
        {
            if (batch.Count == 0)
            {
                return;
            }

            var batched = _bufferedRefreshes ??= [];
            foreach (var change in batch)
            {
                if (_dedupBuffer.Add(change.Key))
                {
                    batched.Add(change);
                }
            }

            _dedupBuffer.Clear();
            _shouldFlushBuffered = true;
        }
    }
}
