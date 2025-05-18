﻿// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

namespace DynamicData.Binding;

/*
 * A much more optimised bind where the sort forms part of the binding.
 *
 * This is much efficient as the prior sort mechanism would resort and clone the entire
 * collection upon every change in order that the sorted list could be transmitted to the bind operator.
 *
 */
internal sealed class SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    // NB: Either comparer or comparerChanged will be used, but not both.

    private readonly Cache<TObject, TKey> _cache = new();
    private readonly IObservable<IChangeSet<TObject, TKey>> _sorted;

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

            var locker = InternalEx.NewLock();
            SortApplicator? sortApplicator = null;

            // Create a new sort applicator each time.
            var latestComparer = comparerChanged.Synchronize(locker)
                .Subscribe(comparer =>
                {
                    sortApplicator = new SortApplicator(_cache, target, comparer, options);
                    sortApplicator.ApplySort();
                });

            // Listen to changes and apply the sorting
            var subscriber = source.Synchronize(locker)
                .Select((changes, index) =>
                {
                    _cache.Clone(changes);

                    // the sort applicator will be null until the comparer change observable fires.
                    if (sortApplicator is not null)
                        sortApplicator.ProcessChanges(changes, index == 0);

                    return changes;
                })
                .SubscribeSafe(observer);

            return new CompositeDisposable(latestComparer, subscriber);
        });

    public IObservable<IChangeSet<TObject, TKey>> Run() => _sorted;

    internal sealed class SortApplicator(
        Cache<TObject, TKey> cache,
        IList<TObject> target,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
    {
        public void ApplySort()
        {
            if (cache.Count == 0) return;

            var fireReset = options.ResetThreshold > 0 && options.ResetThreshold < target.Count;
            var sorted = cache.Items.OrderBy(t => t, comparer);

            Reset(sorted, fireReset);
        }

        // apply sorting as a side effect of the observable stream.
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

        private int GetCurrentPosition(TObject item) =>
            target.GetCurrentPosition(item, comparer, options.UseBinarySearch);

        private int GetInsertPosition(TObject item) =>
            target.GetInsertPosition(item, comparer, options.UseBinarySearch);
    }
}
