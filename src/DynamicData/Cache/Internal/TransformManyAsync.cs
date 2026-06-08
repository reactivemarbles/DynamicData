// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Cache;

namespace DynamicData.Cache.Internal;

internal sealed class TransformManyAsync<TSource, TKey, TDestination, TDestinationKey>(
        IObservable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> transformer,
        IEqualityComparer<TDestination>? equalityComparer,
        IComparer<TDestination>? comparer,
        Action<Error<TSource, TKey>>? errorHandler = null)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() =>
        source.OrchestrateMany<TSource, TKey, IChangeSet<TDestination, TDestinationKey>, IChangeSet<TDestination, TDestinationKey>>(
            (context, emitter) => new Orchestrator(context, emitter, transformer, equalityComparer, comparer, errorHandler));

    private sealed class Orchestrator : OrchestratorCacheChangeBase<TSource, TKey, IChangeSet<TDestination, TDestinationKey>, IChangeSet<TDestination, TDestinationKey>>
    {
        private readonly Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey> _cache = new();
        private readonly ChangeSetMergeTracker<TDestination, TDestinationKey> _tracker;
        private readonly Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> _transformer;
        private readonly Action<Error<TSource, TKey>>? _errorHandler;

        public Orchestrator(
                ICacheOrchestratorContext<TKey, IChangeSet<TDestination, TDestinationKey>> context,
                IObserver<IChangeSet<TDestination, TDestinationKey>> emitter,
                Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> transformer,
                IEqualityComparer<TDestination>? equalityComparer,
                IComparer<TDestination>? comparer,
                Action<Error<TSource, TKey>>? errorHandler)
            : base(context, emitter)
        {
            _transformer = transformer;
            _errorHandler = errorHandler;
            _tracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => _cache.Items, comparer, equalityComparer);
        }

        public override void OnInner(IChangeSet<TDestination, TDestinationKey> child, TKey parentKey) => _tracker.ProcessChangeSet(child, null);

        public override void OnDrainComplete(bool sourcesCompleted) => _tracker.EmitChanges(Emitter);

        protected override void OnItemAdded(TSource item, TKey key) => SubscribeChild(item, key);

        protected override void OnItemUpdated(TSource current, TSource previous, TKey key)
        {
            var prior = _cache.Lookup(key);
            SubscribeChild(current, key);
            if (prior.HasValue)
            {
                _tracker.RemoveItems(prior.Value.Cache.KeyValues);
            }
        }

        protected override void OnItemRemoved(TSource item, TKey key)
        {
            if (_cache.Lookup(key) is { HasValue: true } removed)
            {
                _cache.Remove(key);
                _tracker.RemoveItems(removed.Value.Cache.KeyValues);
            }

            Context.Track(key, null);
        }

        private void SubscribeChild(TSource item, TKey key)
        {
            var entry = new ChangeSetCache<TDestination, TDestinationKey>(Context.Serialize(BuildInner(item, key)));
            _cache.AddOrUpdate(entry, key);
            Context.Track(key, entry.Source);
        }

        private IObservable<IChangeSet<TDestination, TDestinationKey>> BuildInner(TSource obj, TKey key) => _errorHandler is null
            ? Observable.Defer(() => _transformer(obj, key))
            : Observable.Defer(async () =>
            {
                try
                {
                    return await _transformer(obj, key).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _errorHandler(new Error<TSource, TKey>(e, obj, key));
                    return Observable.Empty<IChangeSet<TDestination, TDestinationKey>>();
                }
            });
    }
}
