// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif
/*
 * A much more optimised bind where the sort forms part of the binding.
 *
 * This is much efficient as the prior sort mechanism would resort and clone the entire
 * collection upon every change in order that the sorted list could be transmitted to the bind operator.
 *
 */

/// <summary>
/// Provides members for the SortAndBind class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    // NB: Either comparer or comparerChanged will be used, but not both.

    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly Cache<TObject, TKey> _cache = new();

    /// <summary>
    /// The _sorted field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _sorted;

    /// <summary>
    /// Initializes a new instance of the <see cref="SortAndBind{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <param name="options">The options value.</param>
    /// <param name="target">The target value.</param>
    public SortAndBind(IObservable<IChangeSet<TObject, TKey>> source,
        IComparer<TObject> comparer,
        SortAndBindOptions options,
        IList<TObject> target)
    {
        var scheduler = options.Scheduler;

        // static one time comparer
        var applicator = new SortApplicator(_cache, target, comparer, options);

        if (scheduler is not null)
            source = source.ObserveOn(scheduler);

        _sorted = source.Select((changes, index) =>
        {
            // clone to local cache so that we can sort the entire set when threshold is over a certain size.
            _cache.Clone(changes);

            applicator.ProcessChanges(changes, index == 0);

            return changes;
        });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SortAndBind{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="comparerChanged">The comparerChanged value.</param>
    /// <param name="options">The options value.</param>
    /// <param name="target">The target value.</param>
    public SortAndBind(IObservable<IChangeSet<TObject, TKey>> source,
        IObservable<IComparer<TObject>> comparerChanged,
        SortAndBindOptions options,
        IList<TObject> target)
        => _sorted = Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var scheduler = options.Scheduler;

            if (scheduler is not null)
            {
                source = source.ObserveOn(scheduler);
                comparerChanged = comparerChanged.ObserveOn(scheduler);
            }

            var queue = new SharedDeliveryQueue();
            SortApplicator? sortApplicator = null;

            // Create a new sort applicator each time.
            var latestComparer = comparerChanged.SynchronizeSafe(queue)
                .Subscribe(comparer =>
                {
                    sortApplicator = new SortApplicator(_cache, target, comparer, options);
                    sortApplicator.ApplySort();
                });

            // Listen to changes and apply the sorting
            var subscriber = source.SynchronizeSafe(queue)
                .Select((changes, index) =>
                {
                    _cache.Clone(changes);

                    // the sort applicator will be null until the comparer change observable fires.
                    sortApplicator?.ProcessChanges(changes, index == 0);

                    return changes;
                })
                .SubscribeSafe(observer);

            return new CompositeDisposable(latestComparer, subscriber, queue);
        });

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => _sorted;

/// <summary>
/// Provides members for the SortApplicator class.
/// </summary>
/// <param name="cache">The cache value.</param>
/// <param name="target">The target value.</param>
/// <param name="comparer">The comparer value.</param>
/// <param name="options">The options value.</param>
internal sealed class SortApplicator(
        Cache<TObject, TKey> cache,
        IList<TObject> target,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
    {
        /// <summary>
        /// Executes the ApplySort operation.
        /// </summary>
        public void ApplySort()
        {
            if (cache.Count == 0) return;

            var fireReset = options.ResetThreshold > 0 && options.ResetThreshold < target.Count;
            var sorted = cache.Items.OrderBy(t => t, comparer);

            Reset(sorted, fireReset);
        }
        // apply sorting as a side effect of the observable stream.

        /// <summary>
        /// Executes the ProcessChanges operation.
        /// </summary>
        /// <param name="changeSet">The changeSet value.</param>
        /// <param name="isFirstTimeLoad">The isFirstTimeLoad value.</param>
        public void ProcessChanges(IChangeSet<TObject, TKey> changeSet, bool isFirstTimeLoad)
        {
            var forceReset = isFirstTimeLoad && options.ResetOnFirstTimeLoad;

            // apply sorted changes to the target collection
            if (forceReset || (options.ResetThreshold > 0 && options.ResetThreshold < changeSet.Count))
            {
                Reset(cache.Items.OrderBy(t => t, comparer), true);
            }
            else if (target is ObservableCollectionExtended<TObject> observableCollectionExtended)
            {
                // suspend count as it can result in a flood of binding updates.
                using (observableCollectionExtended.SuspendCount())
                {
                    ApplyChanges(changeSet);
                }
            }
            else
            {
                ApplyChanges(changeSet);
            }
        }

        /// <summary>
        /// Executes the Reset operation.
        /// </summary>
        /// <param name="sorted">The sorted value.</param>
        /// <param name="fireReset">The fireReset value.</param>
        private void Reset(IEnumerable<TObject> sorted, bool fireReset)
        {
            if (fireReset && target is ObservableCollectionExtended<TObject> observableCollectionExtended)
            {
                using (observableCollectionExtended.SuspendNotifications())
                {
                    observableCollectionExtended.Load(sorted);
                }
            }
            else if (fireReset && target is BindingList<TObject> bindingList)
            {
                // suspend count as it can result in a flood of binding updates.
                using (new BindingListEventsSuspender<TObject>(bindingList))
                {
                    target.Clear();
                    target.AddRange(sorted);
                }
            }
            else
            {
                target.Clear();
                foreach (var t in sorted)
                {
                    target.Add(t);
                }
            }
        }

        /// <summary>
        /// Executes the ApplyChanges operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        private void ApplyChanges(IChangeSet<TObject, TKey> changes)
        {
            // iterate through collection, find sorted position and apply changes

            foreach (var change in changes.ToConcreteType())
            {
                var item = change.Current;

                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        {
                            var index = GetInsertPosition(item);
                            target.Insert(index, item);
                        }
                        break;
                    case ChangeReason.Update:
                        {
                            if (!options.UseReplaceForUpdates)
                            {
                                // If using binary search, it works best when we remove then add,
                                // so let's optimise for that first.

                                var currentIndex = GetCurrentPosition(change.Previous.Value);
                                target.RemoveAt(currentIndex);

                                var updatedIndex = GetInsertPosition(item);
                                target.Insert(updatedIndex, item);
                            }
                            else
                            {
                                var currentIndex = GetCurrentPosition(change.Previous.Value);
                                var updatedIndex = GetInsertPosition(item);

                                // We need to recalibrate as GetCurrentPosition includes the current item
                                updatedIndex = currentIndex < updatedIndex ? updatedIndex - 1 : updatedIndex;

                                // Some control suites and platforms do not support replace, whiles others do, so we opt in.
                                if (currentIndex == updatedIndex)
                                {
                                    target[currentIndex] = item;
                                }
                                else
                                {
                                    target.RemoveAt(currentIndex);
                                    target.Insert(updatedIndex, item);
                                }
                            }
                        }
                        break;
                    case ChangeReason.Remove:
                        {
                            var currentIndex = GetCurrentPosition(item);
                            target.RemoveAt(currentIndex);
                        }
                        break;
                    case ChangeReason.Refresh:
                        {
                            /*  look up current location, and new location
                             *
                             *  Use the linear methods as binary search does not work if we do not have an already sorted list.
                             *  Otherwise, SortAndBindWithBinarySearch.Refresh() unit test will break.
                             *
                             * If consumers are using BinarySearch and a refresh event is sent here, they probably should exclude refresh
                             * events with .WhereReasonsAreNot(ChangeReason.Refresh), but it may be problematic to exclude refresh automatically
                             * as that would effectively be swallowing an error.
                             */
                            var currentIndex = target.IndexOf(item);
                            var updatedIndex = target.GetInsertPositionLinear(item, comparer);

                            // We need to recalibrate as GetInsertPosition includes the current item
                            updatedIndex = currentIndex < updatedIndex ? updatedIndex - 1 : updatedIndex;
                            if (updatedIndex != currentIndex)
                            {
                                target.Move(currentIndex, updatedIndex, item);
                            }
                        }
                        break;
                    case ChangeReason.Moved:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Executes the GetCurrentPosition operation.
        /// </summary>
        /// <param name="item">The item value.</param>
        /// <returns>The result of the operation.</returns>
        private int GetCurrentPosition(TObject item) =>
            target.GetCurrentPosition(item, comparer, options.UseBinarySearch);

        /// <summary>
        /// Executes the GetInsertPosition operation.
        /// </summary>
        /// <param name="item">The item value.</param>
        /// <returns>The result of the operation.</returns>
        private int GetInsertPosition(TObject item) =>
            target.GetInsertPosition(item, comparer, options.UseBinarySearch);
    }
}
