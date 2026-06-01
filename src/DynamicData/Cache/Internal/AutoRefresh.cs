// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;

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
        source.OrchestrateMany(new Orchestrator(reEvaluator, buffer, scheduler ?? GlobalConfig.DefaultScheduler))
              .SubscribeSafe(observer));

    private sealed class Orchestrator(
            Func<TObject, TKey, IObservable<TAny>> reEvaluator,
            TimeSpan? buffer,
            IScheduler scheduler)
        : ICacheOrchestrator<TObject, TKey, Change<TObject, TKey>, IChangeSet<TObject, TKey>>
    {
        private readonly HashSet<TKey> _sourceTouched = [];
        private ICacheOrchestratorContext<TKey, Change<TObject, TKey>> _context = null!;
        private IChangeSet<TObject, TKey>? _pendingSource;
        private List<Change<TObject, TKey>>? _pendingRefreshes;
        private IDisposable? _bufferTimer;
        private bool _bufferTimerFired;

        public void Initialize(ICacheOrchestratorContext<TKey, Change<TObject, TKey>> context) => _context = context;

        public void OnSourceChangeSet(IChangeSet<TObject, TKey> changes)
        {
            _pendingSource = _pendingSource is null
                ? changes
                : new ChangeSet<TObject, TKey>(_pendingSource.Concat(changes));

            foreach (var change in changes.ToConcreteType())
            {
                _sourceTouched.Add(change.Key);

                switch (change.Reason)
                {
                    case ChangeReason.Add or ChangeReason.Update:
                        var item = change.Current;
                        var key = change.Key;
                        _context.Track(key, reEvaluator(item, key).Select(_ => new Change<TObject, TKey>(ChangeReason.Refresh, key, item)));
                        break;

                    case ChangeReason.Remove:
                        _context.Track(change.Key, null);
                        break;
                }
            }
        }

        public void OnInner(Change<TObject, TKey> refresh, TKey key)
        {
            // Suppress refresh for any key the source has touched in the same drain.
            // Covers Add+Refresh redundancy (the new value was just delivered) and the
            // Add+Remove+Refresh sequence (item no longer exists, refresh would be invalid).
            if (_sourceTouched.Contains(key))
            {
                return;
            }

            (_pendingRefreshes ??= []).Add(refresh);

            // Buffered mode: defer the flush to a scheduled drain so refreshes accumulate across
            // drains until the window elapses. Bufferless mode flushes on every drain via Emit.
            if (buffer is { } window && _bufferTimer is null)
            {
                _bufferTimer = _context.ScheduleEmit(window, scheduler, MarkBufferFired);
            }
        }

        public void Emit(IObserver<IChangeSet<TObject, TKey>> observer)
        {
            _sourceTouched.Clear();

            var source = _pendingSource;
            _pendingSource = null;
            if (source is { Count: > 0 })
            {
                observer.OnNext(source);
            }

            var shouldFlush = buffer is null || _bufferTimerFired;
            if (!shouldFlush)
            {
                return;
            }

            var refreshes = _pendingRefreshes;
            _pendingRefreshes = null;
            _bufferTimer = null;
            _bufferTimerFired = false;

            if (refreshes is { Count: > 0 })
            {
                observer.OnNext(new ChangeSet<TObject, TKey>(refreshes));
            }
        }

        private void MarkBufferFired() => _bufferTimerFired = true;
    }
}
