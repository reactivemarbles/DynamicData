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
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(observer =>
        source.OrchestrateMany(new Orchestrator(transformer, equalityComparer, comparer, errorHandler))
              .SubscribeSafe(observer));

    private sealed class Orchestrator : ICacheOrchestrator<TSource, TKey, IChangeSet<TDestination, TDestinationKey>, IChangeSet<TDestination, TDestinationKey>>
    {
        private readonly Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey> _cache = new();
        private readonly ChangeSetMergeTracker<TDestination, TDestinationKey> _tracker;
        private readonly Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> _transformer;
        private readonly Action<Error<TSource, TKey>>? _errorHandler;
        private ICacheOrchestratorContext<TKey, IChangeSet<TDestination, TDestinationKey>> _context = null!;

        public Orchestrator(
                Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> transformer,
                IEqualityComparer<TDestination>? equalityComparer,
                IComparer<TDestination>? comparer,
                Action<Error<TSource, TKey>>? errorHandler)
        {
            _transformer = transformer;
            _errorHandler = errorHandler;
            _tracker = new ChangeSetMergeTracker<TDestination, TDestinationKey>(() => _cache.Items, comparer, equalityComparer);
        }

        public void Initialize(ICacheOrchestratorContext<TKey, IChangeSet<TDestination, TDestinationKey>> context) => _context = context;

        public void OnSourceChangeSet(IChangeSet<TSource, TKey> changes)
        {
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add or ChangeReason.Update:
                        var previous = _cache.Lookup(change.Key);
                        var entry = new ChangeSetCache<TDestination, TDestinationKey>(_context.Serialize(BuildInner(change.Current, change.Key)));
                        _cache.AddOrUpdate(entry, change.Key);
                        _context.Track(change.Key, entry.Source);
                        if (previous.HasValue)
                        {
                            _tracker.RemoveItems(previous.Value.Cache.KeyValues);
                        }
                        break;

                    case ChangeReason.Remove:
                        if (_cache.Lookup(change.Key) is { HasValue: true } removed)
                        {
                            _cache.Remove(change.Key);
                            _tracker.RemoveItems(removed.Value.Cache.KeyValues);
                        }

                        _context.Track(change.Key, null);
                        break;
                }
            }
        }

        public void OnInner(IChangeSet<TDestination, TDestinationKey> child, TKey parentKey) => _tracker.ProcessChangeSet(child, null);

        public void Emit(IObserver<IChangeSet<TDestination, TDestinationKey>> observer) => _tracker.EmitChanges(observer);

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
