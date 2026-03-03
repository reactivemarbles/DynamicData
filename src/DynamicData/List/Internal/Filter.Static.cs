// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal static partial class Filter
{
    public static class Static<T>
        where T : notnull
    {
        public static IObservable<IChangeSet<T>> Create(
            IObservable<IChangeSet<T>> source,
            Func<T, bool> predicate,
            bool suppressEmptyChangesets)
        {
            source.ThrowArgumentNullExceptionIfNull(nameof(source));
            predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

            return Observable.Create<IChangeSet<T>>(downstreamObserver =>
            {
                var upstreamItemsStates = new List<(T item, bool isIncluded)>();
                var itemStatesBuffer = new List<(T item, bool isIncluded)>();

                var downstreamItems = new ChangeAwareList<T>();
                var itemsBuffer = new List<T>();

                var downstream = source.Select(upstreamChanges =>
                {
                    foreach (var change in upstreamChanges)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                {
                                    var isIncluded = predicate.Invoke(change.Item.Current);

                                    if (change.Item.CurrentIndex < 0)
                                    {
                                        upstreamItemsStates.Add((
                                            item: change.Item.Current,
                                            isIncluded: isIncluded));

                                        if (isIncluded)
                                            downstreamItems.Add(change.Item.Current);
                                    }
                                    else
                                    {
                                        upstreamItemsStates.Insert(
                                            index: change.Item.CurrentIndex,
                                            item: (
                                                item: change.Item.Current,
                                                isIncluded: isIncluded));

                                        if (isIncluded)
                                        {
                                            downstreamItems.Insert(
                                                index: change.Item.CurrentIndex - CountExcludedItemsBefore(change.Item.CurrentIndex),
                                                item: change.Item.Current);
                                        }
                                    }
                                }
                                break;

                            case ListChangeReason.AddRange:
                                upstreamItemsStates.EnsureCapacity(upstreamItemsStates.Count + change.Range.Count);

                                if ((downstreamItems.Capacity - downstreamItems.Count) < change.Range.Count)
                                    downstreamItems.Capacity = downstreamItems.Count + change.Range.Count;

                                if ((change.Range.Index < 0) || (change.Range.Index == upstreamItemsStates.Count))
                                {
                                    foreach (var item in change.Range)
                                    {
                                        var isIncluded = predicate.Invoke(item);
                                        upstreamItemsStates.Add((
                                            item: item,
                                            isIncluded: isIncluded));

                                        if (isIncluded)
                                            downstreamItems.Add(item);
                                    }
                                }
                                else
                                {
                                    upstreamItemsStates.EnsureCapacity(change.Range.Count);
                                    itemsBuffer.EnsureCapacity(change.Range.Count);

                                    foreach (var item in change.Range)
                                    {
                                        var isIncluded = predicate.Invoke(item);
                                        itemStatesBuffer.Add((
                                            item: item,
                                            isIncluded: isIncluded));

                                        if (isIncluded)
                                            itemsBuffer.Add(item);
                                    }

                                    upstreamItemsStates.InsertRange(
                                        index: change.Range.Index,
                                        collection: itemStatesBuffer);
                                    itemStatesBuffer.Clear();

                                    downstreamItems.InsertRange(
                                        collection: itemsBuffer.ToList(), // .InsertRange() does not perform a defensive copy, so we need to.
                                        index: change.Range.Index - CountExcludedItemsBefore(change.Range.Index));
                                    itemsBuffer.Clear();
                                }
                                break;

                            case ListChangeReason.Clear:
                                upstreamItemsStates.Clear();
                                downstreamItems.Clear();
                                break;

                            case ListChangeReason.Moved:
                                {
                                    var itemState = upstreamItemsStates[change.Item.PreviousIndex];

                                    var downstreamPreviousIndex = change.Item.PreviousIndex - CountExcludedItemsBefore(change.Item.PreviousIndex);

                                    upstreamItemsStates.RemoveAt(change.Item.PreviousIndex);
                                    upstreamItemsStates.Insert(
                                        index: change.Item.CurrentIndex,
                                        item: itemState);

                                    if (itemState.isIncluded)
                                    {
                                        var downstreamCurrentIndex = change.Item.CurrentIndex - CountExcludedItemsBefore(change.Item.CurrentIndex);

                                        if (downstreamPreviousIndex != downstreamCurrentIndex)
                                        {
                                            downstreamItems.Move(
                                                original: downstreamPreviousIndex,
                                                destination: downstreamCurrentIndex);
                                        }
                                    }
                                }
                                break;

                            case ListChangeReason.Refresh:
                                {
                                    var isIncluded = predicate.Invoke(change.Item.Current);

                                    var itemState = upstreamItemsStates[change.Item.CurrentIndex];
                                    upstreamItemsStates[change.Item.CurrentIndex] = itemState with
                                    {
                                        isIncluded = isIncluded
                                    };

                                    var downstreamIndex = (isIncluded || itemState.isIncluded)
                                        ? change.Item.CurrentIndex - CountExcludedItemsBefore(change.Item.CurrentIndex)
                                        : -1;

                                    switch (itemState.isIncluded, isIncluded)
                                    {
                                        case (true, true):
                                            downstreamItems.Refresh(
                                                item: change.Item.Current,
                                                index: downstreamIndex);
                                            break;

                                        case (false, true):
                                            downstreamItems.Insert(
                                                index: downstreamIndex,
                                                item: change.Item.Current);
                                            break;

                                        case (true, false):
                                            downstreamItems.RemoveAt(downstreamIndex);
                                            break;
                                    }
                                }
                                break;

                            case ListChangeReason.Remove:
                                if (upstreamItemsStates[change.Item.CurrentIndex].isIncluded)
                                    downstreamItems.RemoveAt(change.Item.CurrentIndex - CountExcludedItemsBefore(change.Item.CurrentIndex));

                                upstreamItemsStates.RemoveAt(change.Item.CurrentIndex);
                                break;

                            case ListChangeReason.RemoveRange:
                                {
                                    var downstreamIndex = change.Range.Index - CountExcludedItemsBefore(change.Range.Index);

                                    var downstreamCount = 0;
                                    var rangeEnd = change.Range.Index + change.Range.Count;
                                    for (var i = change.Range.Index; i < rangeEnd; ++i)
                                    {
                                        if (upstreamItemsStates[i].isIncluded)
                                            ++downstreamCount;
                                    }

                                    if (downstreamCount is not 0)
                                    {
                                        downstreamItems.RemoveRange(
                                            index: downstreamIndex,
                                            count: downstreamCount);
                                    }

                                    upstreamItemsStates.RemoveRange(
                                        index: change.Range.Index,
                                        count: change.Range.Count);
                                }
                                break;

                            case ListChangeReason.Replace:
                                {
                                    var isIncluded = predicate.Invoke(change.Item.Current);

                                    var itemState = upstreamItemsStates[change.Item.CurrentIndex];
                                    upstreamItemsStates[change.Item.CurrentIndex] = (
                                        item: change.Item.Current,
                                        isIncluded: isIncluded);

                                    var downstreamIndex = (isIncluded || itemState.isIncluded)
                                        ? change.Item.CurrentIndex - CountExcludedItemsBefore(change.Item.CurrentIndex)
                                        : -1;

                                    switch (itemState.isIncluded, isIncluded)
                                    {
                                        case (true, true):
                                            downstreamItems[downstreamIndex] = change.Item.Current;
                                            break;

                                        case (true, false):
                                            downstreamItems.RemoveAt(downstreamIndex);
                                            break;

                                        case (false, true):
                                            downstreamItems.Insert(
                                                index: downstreamIndex,
                                                item: change.Item.Current);
                                            break;
                                    }
                                }
                                break;
                        }
                    }

                    return downstreamItems.CaptureChanges();
                });

                if (suppressEmptyChangesets)
                    downstream = downstream.Where(changes => changes.Count is not 0);

                return downstream.SubscribeSafe(downstreamObserver);

                // This is how we implement order preservation, downstream: each time we do an indexed operation, we
                // count how many excluded items there are before that index, and offset the index by that amount.
                int CountExcludedItemsBefore(int index)
                {
                    var result = 0;
                    for (var i = 0; i < index; ++i)
                    {
                        if (!upstreamItemsStates[i].isIncluded)
                            ++result;
                    }
                    return result;
                }
            });
        }
    }
}
