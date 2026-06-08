// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class AutoRefresh<TObject, TKey, TAny>(
        IObservable<IChangeSet<TObject, TKey>> source,
        Func<TObject, TKey, IObservable<TAny>> reEvaluator,
        TimeSpan? buffer = null,
        IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run()
    {
        var sched = buffer is null ? null : scheduler ?? GlobalConfig.DefaultScheduler;
        return source.OrchestrateMany<TObject, TKey, Change<TObject, TKey>, IChangeSet<TObject, TKey>>(
            (context, emitter) => new Orchestrator(context, emitter, reEvaluator, buffer, sched));
    }

    /// <summary>
    /// Forwards each source changeset to the emitter immediately. Refresh notifications from
    /// per-key reevaluators are accumulated in a dictionary (latest value wins) and flushed at
    /// drain end when <paramref name="buffer"/> is <see langword="null"/>, otherwise via a
    /// single-shot Timer armed by the first pending refresh. Update and Remove on the source drop
    /// any pending refresh for that key, so a Refresh whose value has been obsoleted by a later
    /// source event is never emitted. Refreshes for keys the source has touched within the same
    /// drain cycle are suppressed, so a reevaluator that fires synchronously during item
    /// subscription does not produce a redundant Refresh paired with the Add.
    /// </summary>
    private sealed class Orchestrator(
            ICacheOrchestratorContext<TKey, Change<TObject, TKey>> context,
            IObserver<IChangeSet<TObject, TKey>> emitter,
            Func<TObject, TKey, IObservable<TAny>> reEvaluator,
            TimeSpan? buffer,
            IScheduler? scheduler)
        : OrchestratorCacheChangeBase<TObject, TKey, Change<TObject, TKey>, IChangeSet<TObject, TKey>>(context, emitter), IDisposable
    {
        private readonly Dictionary<TKey, Change<TObject, TKey>> _pendingRefreshes = new();
        private readonly HashSet<TKey> _sourceTouched = [];
        private readonly SerialDisposable _timerSubscription = new();

        public void Dispose() => _timerSubscription.Dispose();

        public override void OnSourceChangeSet(IChangeSet<TObject, TKey> changes)
        {
            base.OnSourceChangeSet(changes);
            if (changes.Count > 0)
            {
                Emitter.OnNext(changes);
            }
        }

        public override void OnInner(Change<TObject, TKey> refresh, TKey key)
        {
            if (_sourceTouched.Contains(key))
            {
                return;
            }

            _pendingRefreshes[key] = refresh;

            if (buffer is { } window)
            {
                _timerSubscription.Disposable ??= Context.Serialize(Observable.Timer(window, scheduler!))
                    .SubscribeSafe(_ => FlushPending(), Emitter.OnError);
            }
        }

        public override void OnDrainComplete(bool isFinal)
        {
            _sourceTouched.Clear();

            // Flush pending refreshes whenever there is no timer-based deferral active
            // (unbuffered) or when this is the final drain (the timer would otherwise be
            // cancelled by stream termination). The queue re-fires OnDrainComplete after any
            // reentrant drain triggered by FlushPending, so a single emit per call suffices.
            if (isFinal || buffer is null)
            {
                FlushPending();
            }
        }

        protected override void OnItemAdded(TObject item, TKey key)
        {
            _sourceTouched.Add(key);
            Context.Track(key, reEvaluator(item, key).Select(_ => new Change<TObject, TKey>(ChangeReason.Refresh, key, item)));
        }

        protected override void OnItemUpdated(TObject current, TObject previous, TKey key)
        {
            DropPending(key);
            OnItemAdded(current, key);
        }

        protected override void OnItemRemoved(TObject item, TKey key)
        {
            _sourceTouched.Add(key);
            DropPending(key);
            Context.Track(key, null);
        }

        protected override void OnItemRefreshed(TObject item, TKey key) => _sourceTouched.Add(key);

        private void DropPending(TKey key)
        {
            if (_pendingRefreshes.Remove(key) && _pendingRefreshes.Count == 0)
            {
                _timerSubscription.Disposable = null;
            }
        }

        private void FlushPending()
        {
            _timerSubscription.Disposable = null;

            if (_pendingRefreshes.Count == 0)
            {
                return;
            }

            var batch = new ChangeSet<TObject, TKey>(_pendingRefreshes.Values);
            _pendingRefreshes.Clear();
            Emitter.OnNext(batch);
        }
    }
}
