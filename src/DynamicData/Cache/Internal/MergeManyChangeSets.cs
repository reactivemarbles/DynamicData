// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

/// <summary>
/// ChangeSet Aware MergeMany operator.
/// </summary>
internal sealed class MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    private readonly Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> _changeSetSelector;

    private readonly IComparer<TDestination>? _comparer;

    private readonly IEqualityComparer<TDestination>? _equalityComparer;

    public MergeManyChangeSets(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> selector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    {
        _source = source;
        _changeSetSelector = selector;
        _comparer = comparer;
        _equalityComparer = equalityComparer;
    }

    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run()
    {
        return Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
            observer =>
            {
                var locker = new object();

                // Transform to a merge container.
                var sourceCaches = _source.Synchronize(locker)
                                            .IgnoreSameReferenceUpdate()
                                            .Transform((obj, key) => new MergeContainer(_changeSetSelector(obj, key)))
                                            .AsObservableCache();

                // this is manages all of the changes
                var changeTracker = new ChangeTracker(sourceCaches, _comparer, _equalityComparer);

                var shared = sourceCaches.Connect().Publish();

                // merge the items back together
                var allChanges = shared.MergeMany(mc => mc.Source).Synchronize(locker).Subscribe(
                    changes =>
                    {
                        changeTracker.ProcessChangeSet(changes);

                        changeTracker.EmitChanges(observer);
                    });

                // when an item is removed, all of its sub-items need to be checked
                var removedItem = shared.OnItemRemoved(
                    mc =>
                    {
                        // Remove items if required
                        changeTracker.RemoveItems(mc.Cache.KeyValues);

                        changeTracker.EmitChanges(observer);
                    }).Subscribe();

                // when an item is updated, all of the sub-items from the previous value need to be checked
                var updateItem = shared.OnItemUpdated(
                    (_, prev) =>
                    {
                        // Remove items from the previous value
                        changeTracker.RemoveItems(prev.Cache.KeyValues);

                        changeTracker.EmitChanges(observer);
                    }).Subscribe();

                return new CompositeDisposable(sourceCaches, allChanges, removedItem, updateItem, shared.Connect());
            });
    }

    private class ChangeTracker
    {
        private readonly ChangeAwareCache<TDestination, TDestinationKey> _resultCache;
        private readonly IObservableCache<MergeContainer, TKey> _sourceCache;
        private readonly IComparer<TDestination>? _comparer;
        private readonly IEqualityComparer<TDestination>? _equalityComparer;

        public ChangeTracker(IObservableCache<MergeContainer, TKey> sourceCache, IComparer<TDestination>? comparer, IEqualityComparer<TDestination>? equalityComparer)
        {
            _resultCache = new ChangeAwareCache<TDestination, TDestinationKey>();
            _sourceCache = sourceCache;
            _comparer = comparer;
            _equalityComparer = equalityComparer;
        }

        public void EmitChanges(IObserver<IChangeSet<TDestination, TDestinationKey>> observer)
        {
            var changeSet = _resultCache.CaptureChanges();
            if (changeSet.Count != 0)
            {
                observer.OnNext(changeSet);
            }
        }

        public void RemoveItems(IEnumerable<KeyValuePair<TDestinationKey, TDestination>> items)
        {
            var sourceCaches = _sourceCache.Items.ToArray();

            // Update the Published Value for each item being removed
            if (items is IList<KeyValuePair<TDestinationKey, TDestination>> list)
            {
                // zero allocation enumerator
                foreach (var item in EnumerableIList.Create(list))
                {
                    OnItemRemoved(sourceCaches, item.Value, item.Key);
                }
            }
            else
            {
                foreach (var item in items)
                {
                    OnItemRemoved(sourceCaches, item.Value, item.Key);
                }
            }
        }

        public void ProcessChangeSet(IChangeSet<TDestination, TDestinationKey> changes)
        {
            var sourceCaches = _sourceCache.Items.ToArray();

            foreach (var change in changes.ToConcreteType())
            {
                if (change.Reason == ChangeReason.Add)
                {
                    OnItemAdded(change.Current, change.Key);
                }
                else if (change.Reason == ChangeReason.Update)
                {
                    OnItemUpdated(sourceCaches, change.Key, change.Previous);
                }
                else if (change.Reason == ChangeReason.Remove)
                {
                    OnItemRemoved(sourceCaches, change.Current, change.Key);
                }
                else if (change.Reason == ChangeReason.Refresh)
                {
                    OnItemRefreshed(sourceCaches, change.Current, change.Key);
                }
            }
        }

        private void OnItemAdded(TDestination item, TDestinationKey key)
        {
            var cached = _resultCache.Lookup(key);

            // If no current value or the new value is better, add or update
            if (!cached.HasValue || CheckCandidate(item, cached.Value))
            {
                _resultCache.AddOrUpdate(item, key);
            }
        }

        private void OnItemRemoved(MergeContainer[] sourceCaches, TDestination item, TDestinationKey key)
        {
            var cached = _resultCache.Lookup(key);

            Debug.Assert(cached.HasValue, "Should have a value if it is being removed");

            // Determine if the value being removed is the currently published value for this key
            if (!cached.HasValue || CheckEquality(item, cached.Value))
            {
                PublishBestCandidate(sourceCaches, key, cached);
            }
        }

        private void OnItemUpdated(MergeContainer[] sources, TDestinationKey key, Optional<TDestination> prev)
        {
            var cached = _resultCache.Lookup(key);

            // Previous and Cached should have a value if there is an update happening
            // If the values are the same, then the currently published value is being replaced so select a new value to publish.
            if (!prev.HasValue || !cached.HasValue || CheckEquality(prev.Value, cached.Value))
            {
                PublishBestCandidate(sources, key, cached);
            }
        }

        private void OnItemRefreshed(MergeContainer[] sources, TDestination item, TDestinationKey key)
        {
            var cached = _resultCache.Lookup(key);

            // If published value doesn't change after a refresh AND the published value is the one being refreshed
            if (!PublishBestCandidate(sources, key, cached) && CheckEquality(cached.Value, item))
            {
                // Emit the refresh downstream
                _resultCache.Refresh(key);
            }
        }

        private bool PublishBestCandidate(MergeContainer[] sources, TDestinationKey key, Optional<TDestination> current)
        {
            // It is, so see if there is a candidate value or if it should be removed
            var candidate = FindCandidate(sources, key);
            if (candidate.HasValue)
            {
                // If there isn't a current value or if the candidate is different
                if (!current.HasValue || !CheckEquality(current.Value, candidate.Value))
                {
                    _resultCache.AddOrUpdate(candidate.Value, key);
                    return true;
                }

                // The currently published one is already the best choice
                return false;
            }

            // No best candidate available
            _resultCache.Remove(key);
            return true;
        }

        private Optional<TDestination> FindCandidate(MergeContainer[] sources, TDestinationKey key)
        {
            if (sources.Length == 0)
            {
                return Optional.None<TDestination>();
            }

            var values = sources.Select(s => s.Cache.Lookup(key)).Where(opt => opt.HasValue);

            if (_comparer is not null)
            {
                values = values.OrderBy(opt => opt.Value, _comparer);
            }

            return values.FirstOrDefault();
        }

        private bool CheckEquality(TDestination left, TDestination right) =>
            ReferenceEquals(left, right) || (_equalityComparer?.Equals(left, right) ?? (_comparer?.Compare(left, right) == 0));

        // Return true if candidate is better than the current one.
        // Return false if they're the same not otherwise not better.
        private bool CheckCandidate(TDestination candidate, TDestination current) =>
            !ReferenceEquals(candidate, current) && (_comparer?.Compare(candidate, current) > 0);
    }

    private class MergeContainer
    {
        public MergeContainer(IObservable<IChangeSet<TDestination, TDestinationKey>> source)
        {
            Source = source.Do(Clone);
        }

        public Cache<TDestination, TDestinationKey> Cache { get; } = new();

        public IObservable<IChangeSet<TDestination, TDestinationKey>> Source { get; }

        private void Clone(IChangeSet<TDestination, TDestinationKey> changes)
        {
            Cache.Clone(changes);
        }
    }
}
