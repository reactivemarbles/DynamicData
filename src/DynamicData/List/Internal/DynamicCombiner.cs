// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Cache.Internal;
using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class DynamicCombiner<T>(IObservableList<IObservable<IChangeSet<T>>> source, CombineOperator type)
    where T : notnull
{
    private readonly object _locker = new();

    private readonly IObservableList<IObservable<IChangeSet<T>>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                // this is the resulting list which produces all notifications
                var resultList = new ChangeAwareListWithRefCounts<T>();

                // Transform to a merge container.
                // This populates a RefTracker when the original source is subscribed to
                var sourceLists = _source.Connect().Synchronize(_locker).Transform(changeSet => new MergeContainer(changeSet)).AsObservableList();

                // merge the items back together
                var allChanges = sourceLists.Connect().MergeMany(mc => mc.Source).Synchronize(_locker).Subscribe(
                    changes =>
                    {
                        // Populate result list and check for changes
                        var notifications = UpdateResultList(sourceLists.Items.AsArray(), resultList, changes);
                        if (notifications.Count != 0)
                        {
                            observer.OnNext(notifications);
                        }
                    });

                // When a list is removed, update all items that were in that list
                var removedItem = sourceLists.Connect().OnItemRemoved(
                    mc =>
                    {
                        // Remove items if required
                        var notifications = UpdateItemSetMemberships(sourceLists.Items.AsArray(), resultList, mc.Tracker.Items);
                        if (notifications.Count != 0)
                        {
                            observer.OnNext(notifications);
                        }

                        // On some operators, items not in the removed list can also be affected.
                        if (type == CombineOperator.And || type == CombineOperator.Except)
                        {
                            var itemsToCheck = sourceLists.Items.SelectMany(mc2 => mc2.Tracker.Items).ToArray();
                            var notification2 = UpdateItemSetMemberships(sourceLists.Items.AsArray(), resultList, itemsToCheck);
                            if (notification2.Count != 0)
                            {
                                observer.OnNext(notification2);
                            }
                        }
                    }).Subscribe();

                // When a list is added, update all items that are in that list
                var sourceChanged = sourceLists.Connect().WhereReasonsAre(ListChangeReason.Add, ListChangeReason.AddRange).ForEachItemChange(
                    mc =>
                    {
                        var notifications = UpdateItemSetMemberships(sourceLists.Items.AsArray(), resultList, mc.Current.Tracker.Items);
                        if (notifications.Count != 0)
                        {
                            observer.OnNext(notifications);
                        }

                        // On some operators, items not in the new list can also be affected.
                        if (type == CombineOperator.And || type == CombineOperator.Except)
                        {
                            var notification2 = UpdateItemSetMemberships(sourceLists.Items.AsArray(), resultList, resultList.ToArray());
                            if (notification2.Count != 0)
                            {
                                observer.OnNext(notification2);
                            }
                        }
                    }).Subscribe();

                return new CompositeDisposable(sourceLists, allChanges, removedItem, sourceChanged);
            });

    private bool MatchesConstraint(MergeContainer[] sourceLists, T item)
    {
        if (sourceLists.Length == 0)
        {
            return false;
        }

        switch (type)
        {
            case CombineOperator.And:
                {
                    return sourceLists.All(s => s.Tracker.Contains(item));
                }

            case CombineOperator.Or:
                {
                    return sourceLists.Any(s => s.Tracker.Contains(item));
                }

            case CombineOperator.Xor:
                {
                    return sourceLists.Count(s => s.Tracker.Contains(item)) == 1;
                }

            case CombineOperator.Except:
                {
                    var first = sourceLists[0].Tracker.Contains(item);
                    var others = sourceLists.Skip(1).Any(s => s.Tracker.Contains(item));
                    return first && !others;
                }

            default:
                throw new IndexOutOfRangeException("Unknown CombineOperator " + type);
        }
    }

    private void UpdateItemMembership(T item, MergeContainer[] sourceLists, ChangeAwareListWithRefCounts<T> resultList)
    {
        var isInResult = resultList.Contains(item);
        var shouldBeInResult = MatchesConstraint(sourceLists, item);
        if (shouldBeInResult && !isInResult)
        {
            resultList.Add(item);
        }
        else if (!shouldBeInResult && isInResult)
        {
            resultList.Remove(item);
        }
    }

    private IChangeSet<T> UpdateItemSetMemberships(MergeContainer[] sourceLists, ChangeAwareListWithRefCounts<T> resultingList, IEnumerable<T> items)
    {
        items.ForEach(item => UpdateItemMembership(item, sourceLists, resultingList));
        return resultingList.CaptureChanges();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "By Design.")]
    private IChangeSet<T> UpdateResultList(MergeContainer[] sourceLists, ChangeAwareListWithRefCounts<T> resultList, IChangeSet<T> changes)
    {
        // child caches have been updated before we reached this point.
        foreach (var change in changes.Flatten())
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                case ListChangeReason.Remove:
                    UpdateItemMembership(change.Current, sourceLists, resultList);
                    break;

                case ListChangeReason.Replace:
                    UpdateItemMembership(change.Previous.Value, sourceLists, resultList);
                    UpdateItemMembership(change.Current, sourceLists, resultList);
                    break;

                // Pass through refresh changes:
                case ListChangeReason.Refresh:
                    resultList.Refresh(change.Current);
                    break;

                // A move does not affect contents and so can be ignored:
                case ListChangeReason.Moved:
                    break;

                // These should not occur as they are replaced by the Flatten operator:
                //// case ListChangeReason.AddRange:
                //// case ListChangeReason.RemoveRange:
                //// case ListChangeReason.Clear:

                default:
                    throw new ArgumentOutOfRangeException(nameof(change.Reason), "Unsupported change type");
            }
        }

        return resultList.CaptureChanges();
    }

    private sealed class MergeContainer
    {
        public MergeContainer(IObservable<IChangeSet<T>> source) => Source = source.Do(Clone);

        public IObservable<IChangeSet<T>> Source { get; }

        public ReferenceCountTracker<T> Tracker { get; } = new();

        private void Clone(IChangeSet<T> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        Tracker.Add(change.Item.Current);
                        break;

                    case ListChangeReason.AddRange:
                        foreach (var t in change.Range)
                        {
                            Tracker.Add(t);
                        }

                        break;

                    case ListChangeReason.Replace:
                        Tracker.Remove(change.Item.Previous.Value);
                        Tracker.Add(change.Item.Current);
                        break;

                    case ListChangeReason.Remove:
                        Tracker.Remove(change.Item.Current);
                        break;

                    case ListChangeReason.RemoveRange:
                    case ListChangeReason.Clear:
                        foreach (var t in change.Range)
                        {
                            Tracker.Remove(t);
                        }

                        break;
                }
            }
        }
    }
}
