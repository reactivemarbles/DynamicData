// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal static partial class IntObservableCacheEx
{
    /// <summary>
    /// Orchestrates per-key inner observables that drive mutations to a shared
    /// <see cref="ChangeAwareCache{TOutput, TKey}"/>. Source changeset events and inner emissions
    /// are coalesced into a single downstream changeset per drain cycle. Specialization of
    /// <see cref="OrchestrateMany{TSource, TKey, TInner, TResult}(IObservable{IChangeSet{TSource, TKey}}, ICacheOrchestrator{TSource, TKey, TInner, TResult})"/>
    /// for the "mirror manipulator" shape used by FilterOnObservable, TransformOnObservable, and
    /// similar operators where each source key contributes 0 or 1 items to the output.
    /// </summary>
    /// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
    /// <typeparam name="TKey">Type of the source changeset key (also the key of the output changeset).</typeparam>
    /// <typeparam name="TInner">Value type emitted by the per-key inner observable.</typeparam>
    /// <typeparam name="TOutput">Type of items in the output changeset.</typeparam>
    /// <param name="source">The keyed source changeset stream.</param>
    /// <param name="innerFactory">Builds the per-key inner observable from the source item and its key.</param>
    /// <param name="onSourceChange">
    /// Invoked once per source change. Receives the output cache and the source change; the caller
    /// decides how (and whether) the source event mutates the output cache. Always invoked before
    /// the corresponding inner subscription is created or torn down.
    /// </param>
    /// <param name="onInner">
    /// Invoked once per inner emission. Receives the output cache, the source key, the current source
    /// item, and the value emitted by the inner observable. The caller decides how the inner emission
    /// mutates the output cache.
    /// </param>
    /// <returns>An observable changeset where every emission is the captured changes from one drain cycle.</returns>
    public static IObservable<IChangeSet<TOutput, TKey>> OrchestrateManyChanges<TSource, TKey, TInner, TOutput>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TKey, IObservable<TInner>> innerFactory,
            Action<ChangeAwareCache<TOutput, TKey>, Change<TSource, TKey>> onSourceChange,
            Action<ChangeAwareCache<TOutput, TKey>, TKey, TSource, TInner> onInner)
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
        where TOutput : notnull =>
        Observable.Create<IChangeSet<TOutput, TKey>>(observer =>
            source.OrchestrateMany(new ChangesOrchestrator<TSource, TKey, TInner, TOutput>(innerFactory, onSourceChange, onInner))
                  .SubscribeSafe(observer));

    private sealed class ChangesOrchestrator<TSource, TKey, TInner, TOutput>(Func<TSource, TKey, IObservable<TInner>> innerFactory, Action<ChangeAwareCache<TOutput, TKey>, Change<TSource, TKey>> onSourceChange, Action<ChangeAwareCache<TOutput, TKey>, TKey, TSource, TInner> onInner)
        : OrchestratorCacheChangeBase<TSource, TKey, (TSource Item, TInner Value), IChangeSet<TOutput, TKey>>
        where TSource : notnull
        where TKey : notnull
        where TInner : notnull
        where TOutput : notnull
    {
        private readonly ChangeAwareCache<TOutput, TKey> _cache = new();

        public override void OnInner((TSource Item, TInner Value) value, TKey key) => onInner(_cache, key, value.Item, value.Value);

        public override void OnDrainComplete()
        {
            while (true)
            {
                var captured = _cache.CaptureChanges();
                if (captured.Count == 0)
                {
                    break;
                }

                Emitter.OnNext(captured);
            }
        }

        protected override void OnItemAdded(TSource item, TKey key)
        {
            onSourceChange(_cache, new Change<TSource, TKey>(ChangeReason.Add, key, item));
            Context.Track(key, innerFactory(item, key).Select(value => (Item: item, Value: value)));
        }

        protected override void OnItemUpdated(TSource current, TSource previous, TKey key)
        {
            onSourceChange(_cache, new Change<TSource, TKey>(ChangeReason.Update, key, current, previous));
            Context.Track(key, innerFactory(current, key).Select(value => (Item: current, Value: value)));
        }

        protected override void OnItemRemoved(TSource item, TKey key)
        {
            onSourceChange(_cache, new Change<TSource, TKey>(ChangeReason.Remove, key, item));
            Context.Track(key, null);
        }

        protected override void OnItemRefreshed(TSource item, TKey key) =>
            onSourceChange(_cache, new Change<TSource, TKey>(ChangeReason.Refresh, key, item));
    }
}
