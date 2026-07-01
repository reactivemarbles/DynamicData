// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the DynamicCombiner class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="type">The type value.</param>
internal sealed class DynamicCombiner<TObject, TKey>(IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservableList<IObservable<IChangeSet<TObject, TKey>>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();

                // this is the resulting cache which produces all notifications
                var resultCache = new ChangeAwareCache<TObject, TKey>();

                // Transform to a merge container.
                // This populates a RefTracker when the original source is subscribed to
                var sourceLists = _source.Connect().SynchronizeSafe(queue).Transform(changeSet => new MergeContainer(changeSet)).AsObservableList();

                var sharedLists = sourceLists.Connect().Publish();

                // merge the items back together
                var allChanges = sharedLists.MergeMany(mc => mc.Source).SynchronizeSafe(queue).Subscribe(
                    changes =>
                    {
                        // Populate result list and check for changes
                        UpdateResultList(resultCache, sourceLists.Items.AsArray(), changes);

                        var notifications = resultCache.CaptureChanges();
                        if (notifications.Count != 0)
                        {
                            observer.OnNext(notifications);
                        }
                    });

                // when an list is removed, need to
                var removedItem = sharedLists.OnItemRemoved(
                    mc =>
                    {
                        // Remove items if required
                        ProcessChanges(resultCache, sourceLists.Items.AsArray(), mc.Cache.KeyValues);

                        if (type == CombineOperator.And || type == CombineOperator.Except)
                        {
                            var itemsToCheck = sourceLists.Items.SelectMany(mc2 => mc2.Cache.KeyValues);
                            ProcessChanges(resultCache, sourceLists.Items.AsArray(), itemsToCheck);
                        }

                        var notifications = resultCache.CaptureChanges();
                        if (notifications.Count != 0)
                        {
                            observer.OnNext(notifications);
                        }
                    }).Subscribe();

                // when an list is added or removed, need to
                var sourceChanged = sharedLists.WhereReasonsAre(ListChangeReason.Add, ListChangeReason.AddRange).ForEachItemChange(
                    mc =>
                    {
                        ProcessChanges(resultCache, sourceLists.Items.AsArray(), mc.Current.Cache.KeyValues);

                        if (type == CombineOperator.And || type == CombineOperator.Except)
                        {
                            ProcessChanges(resultCache, sourceLists.Items.AsArray(), resultCache.KeyValues.ToArray());
                        }

                        var notifications = resultCache.CaptureChanges();
                        if (notifications.Count != 0)
                        {
                            observer.OnNext(notifications);
                        }
                    }).Subscribe();

                return new CompositeDisposable(sourceLists, allChanges, removedItem, sourceChanged, sharedLists.Connect(), queue);
            });

    /// <summary>
    /// Executes the MatchesConstraint operation.
    /// </summary>
    /// <param name="sources">The sources value.</param>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    private bool MatchesConstraint(MergeContainer[] sources, TKey key)
    {
        if (sources.Length == 0)
        {
            return false;
        }

        switch (type)
        {
            case CombineOperator.And:
                {
                    return sources.All(s => s.Cache.Lookup(key).HasValue);
                }

            case CombineOperator.Or:
                {
                    return sources.Any(s => s.Cache.Lookup(key).HasValue);
                }

            case CombineOperator.Xor:
                {
                    return sources.Count(s => s.Cache.Lookup(key).HasValue) == 1;
                }

            case CombineOperator.Except:
                {
                    var first = sources.Take(1).Any(s => s.Cache.Lookup(key).HasValue);
                    var others = sources.Skip(1).Any(s => s.Cache.Lookup(key).HasValue);
                    return first && !others;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(key));
        }
    }

    /// <summary>
    /// Executes the ProcessChanges operation.
    /// </summary>
    /// <param name="target">The target value.</param>
    /// <param name="sourceLists">The sourceLists value.</param>
    /// <param name="items">The items value.</param>
    private void ProcessChanges(ChangeAwareCache<TObject, TKey> target, MergeContainer[] sourceLists, IEnumerable<KeyValuePair<TKey, TObject>> items)
    {
        // check whether the item should be removed from the list (or in the case of And, added)
        if (items is IList<KeyValuePair<TKey, TObject>> list)
        {
            // zero allocation enumerator
            foreach (var item in EnumerableIList.Create(list))
            {
                ProcessItem(target, sourceLists, item.Value, item.Key);
            }
        }
        else
        {
            foreach (var item in items)
            {
                ProcessItem(target, sourceLists, item.Value, item.Key);
            }
        }
    }

    /// <summary>
    /// Executes the ProcessItem operation.
    /// </summary>
    /// <param name="target">The target value.</param>
    /// <param name="sourceLists">The sourceLists value.</param>
    /// <param name="item">The item value.</param>
    /// <param name="key">The key value.</param>
    private void ProcessItem(ChangeAwareCache<TObject, TKey> target, MergeContainer[] sourceLists, TObject item, TKey key)
    {
        var cached = target.Lookup(key);
        var shouldBeInResult = MatchesConstraint(sourceLists, key);

        if (shouldBeInResult)
        {
            if (!cached.HasValue)
            {
                target.AddOrUpdate(item, key);
            }
            else if (!ReferenceEquals(item, cached.Value))
            {
                target.AddOrUpdate(item, key);
            }
        }
        else if (cached.HasValue)
        {
            target.Remove(key);
        }
    }

    /// <summary>
    /// Executes the UpdateResultList operation.
    /// </summary>
    /// <param name="target">The target value.</param>
    /// <param name="sourceLists">The sourceLists value.</param>
    /// <param name="changes">The changes value.</param>
    private void UpdateResultList(ChangeAwareCache<TObject, TKey> target, MergeContainer[] sourceLists, IChangeSet<TObject, TKey> changes)
    {
        foreach (var change in changes.ToConcreteType())
        {
            ProcessItem(target, sourceLists, change.Current, change.Key);
        }
    }

/// <summary>
/// Provides members for the MergeContainer class.
/// </summary>
private sealed class MergeContainer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergeContainer"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        public MergeContainer(IObservable<IChangeSet<TObject, TKey>> source) => Source = source.Do(Clone);

        /// <summary>
        /// Gets the Cache value.
        /// </summary>
        public Cache<TObject, TKey> Cache { get; } = new();

        /// <summary>
        /// Gets the Source value.
        /// </summary>
        public IObservable<IChangeSet<TObject, TKey>> Source { get; }

        /// <summary>
        /// Executes the Clone operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        private void Clone(IChangeSet<TObject, TKey> changes) => Cache.Clone(changes);
    }
}
