// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.List.Internal;

internal static partial class Filter
{
    public static class WithPredicateState<T, TState>
        where T : notnull
    {
        public static IObservable<IChangeSet<T>> Create(
            IObservable<IChangeSet<T>> source,
            IObservable<TState> predicateState,
            Func<TState, T, bool> predicate,
            ListFilterPolicy filterPolicy = ListFilterPolicy.CalculateDiff,
            bool suppressEmptyChangeSets = true)
        {
            source.ThrowArgumentNullExceptionIfNull(nameof(source));
            predicateState.ThrowArgumentNullExceptionIfNull(nameof(predicateState));
            predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

            if (!EnumEx.IsDefined(filterPolicy))
                throw new ArgumentException($"Invalid {nameof(ListFilterPolicy)} value {filterPolicy}");

            return Observable.Create<IChangeSet<T>>(observer =>
            {
                SubscriptionBase subscription = (filterPolicy is ListFilterPolicy.CalculateDiff)
                    ? new CalculateDiffSubscription(
                        downstreamObserver: observer,
                        predicate: predicate,
                        suppressEmptyChangeSets: suppressEmptyChangeSets)
                    : new ClearAndReplaceSubscription(
                        downstreamObserver: observer,
                        predicate: predicate,
                        suppressEmptyChangeSets: suppressEmptyChangeSets);

                subscription.Activate(
                    predicateState: predicateState,
                    source: source);

                return subscription;
            });
        }

        private abstract class SubscriptionBase
            : IDisposable
        {
            private readonly List<Change<T>> _downstreamChangesBuffer;
            private readonly IObserver<IChangeSet<T>> _downstreamObserver;
            private readonly List<T> _itemsBuffer;
            private readonly List<ItemState> _itemStates;
            private readonly List<ItemState> _itemStatesBuffer;
            private readonly Func<TState, T, bool> _predicate;
            private readonly bool _suppressEmptyChangeSets;

            private bool _hasPredicateStateCompleted;
            private bool _hasSourceCompleted;
            private bool _isLatestPredicateStateValid;
            private TState _latestPredicateState;
            private IDisposable? _predicateStateSubscription;
            private IDisposable? _sourceSubscription;

            protected SubscriptionBase(
                IObserver<IChangeSet<T>> downstreamObserver,
                Func<TState, T, bool> predicate,
                bool suppressEmptyChangeSets)
            {
                _downstreamObserver = downstreamObserver;
                _predicate = predicate;
                _suppressEmptyChangeSets = suppressEmptyChangeSets;

                _downstreamChangesBuffer = new();
                _itemsBuffer = new();
                _itemStates = new();
                _itemStatesBuffer = new();
                _latestPredicateState = default!;
            }

            // Keeping subscriptions out of the constructor prevents subscriptions that emit immediately from triggering virtual method calls within the constructor.
            public void Activate(
                IObservable<TState> predicateState,
                IObservable<IChangeSet<T>> source)
            {
                var onError = OnError;

                _predicateStateSubscription = predicateState
                    .SubscribeSafe(
                        onNext: OnPredicateStateNext,
                        onError: onError,
                        onCompleted: OnPredicateStateCompleted);

                _sourceSubscription = source
                    .SubscribeSafe(
                        onNext: OnSourceNext,
                        onError: onError,
                        onCompleted: OnSourceCompleted);
            }

            public void Dispose()
            {
                _predicateStateSubscription?.Dispose();
                _sourceSubscription?.Dispose();
            }

            protected List<Change<T>> DownstreamChangesBuffer
                => _downstreamChangesBuffer;

            protected bool IsLatestPredicateStateValid
                => _isLatestPredicateStateValid;

            protected List<T> ItemsBuffer
                => _itemsBuffer;

            protected List<ItemState> ItemStates
                => _itemStates;

            protected List<ItemState> ItemStatesBuffer
                => _itemStatesBuffer;

            protected TState LatestPredicateState
                => _latestPredicateState;

            protected Func<TState, T, bool> Predicate
                => _predicate;

            protected abstract void PerformAdd(ItemChange<T> change);

            protected abstract void PerformAddRange(RangeChange<T> change);

            protected abstract void PerformClear();

            protected abstract void PerformMove(ItemChange<T> change);

            protected abstract void PerformReFilter();

            protected abstract void PerformRefresh(ItemChange<T> change);

            protected abstract void PerformRemove(ItemChange<T> change);

            protected abstract void PerformRemoveRange(RangeChange<T> change);

            protected abstract void PerformReplace(ItemChange<T> change);

            private object DownstreamSynchronizationGate
                => _downstreamChangesBuffer;

            private object UpstreamSynchronizationGate
                => _itemStates;

            private IChangeSet<T> AssembleDownstreamChanges()
            {
                if (_downstreamChangesBuffer.Count is 0)
                    return ChangeSet<T>.Empty;

                var downstreamChanges = new ChangeSet<T>(_downstreamChangesBuffer);
                _downstreamChangesBuffer.Clear();

                return downstreamChanges;
            }

            private void OnError(Exception error)
            {
                var hasUpstreamLock = false;
                var hasDownstreamLock = false;
                try
                {
                    Monitor.Enter(UpstreamSynchronizationGate, ref hasUpstreamLock);

                    _predicateStateSubscription?.Dispose();
                    _sourceSubscription?.Dispose();

                    Monitor.Enter(DownstreamSynchronizationGate, ref hasDownstreamLock);

                    if (hasUpstreamLock)
                    {
                        Monitor.Exit(UpstreamSynchronizationGate);
                        hasUpstreamLock = false;
                    }

                    _downstreamObserver.OnError(error);
                }
                finally
                {
                    if (hasUpstreamLock)
                        Monitor.Exit(UpstreamSynchronizationGate);

                    if (hasDownstreamLock)
                        Monitor.Exit(DownstreamSynchronizationGate);
                }
            }

            private void OnPredicateStateCompleted()
            {
                var hasUpstreamLock = false;
                var hasDownstreamLock = false;
                try
                {
                    Monitor.Enter(UpstreamSynchronizationGate, ref hasUpstreamLock);

                    _hasPredicateStateCompleted = true;

                    // If we didn't get at least one predicateState value, we can't ever emit any (non-empty) downstream changesets,
                    // no matter how many items come through from source, so just go ahead and complete now.
                    if (_hasSourceCompleted || (!_isLatestPredicateStateValid && _suppressEmptyChangeSets))
                    {
                        Monitor.Enter(DownstreamSynchronizationGate, ref hasDownstreamLock);

                        if (hasUpstreamLock)
                        {
                            Monitor.Exit(UpstreamSynchronizationGate);
                            hasUpstreamLock = false;
                        }

                        _downstreamObserver.OnCompleted();
                    }
                }
                finally
                {
                    if (hasUpstreamLock)
                        Monitor.Exit(UpstreamSynchronizationGate);

                    if (hasDownstreamLock)
                        Monitor.Exit(DownstreamSynchronizationGate);
                }
            }

            private void OnPredicateStateNext(TState predicateState)
            {
                var hasUpstreamLock = false;
                var hasDownstreamLock = false;
                try
                {
                    Monitor.Enter(UpstreamSynchronizationGate, ref hasUpstreamLock);

                    _latestPredicateState = predicateState;
                    _isLatestPredicateStateValid = true;

                    PerformReFilter();

                    var downstreamChanges = AssembleDownstreamChanges();

                    if ((downstreamChanges.Count is not 0) || !_suppressEmptyChangeSets)
                    {
                        Monitor.Enter(DownstreamSynchronizationGate, ref hasDownstreamLock);

                        if (hasUpstreamLock)
                        {
                            Monitor.Exit(UpstreamSynchronizationGate);
                            hasUpstreamLock = false;
                        }

                        _downstreamObserver.OnNext(downstreamChanges);
                    }
                }
                finally
                {
                    if (hasUpstreamLock)
                        Monitor.Exit(UpstreamSynchronizationGate);

                    if (hasDownstreamLock)
                        Monitor.Exit(DownstreamSynchronizationGate);
                }
            }

            private void OnSourceCompleted()
            {
                var hasUpstreamLock = false;
                var hasDownstreamLock = false;
                try
                {
                    Monitor.Enter(UpstreamSynchronizationGate, ref hasUpstreamLock);

                    _hasSourceCompleted = true;

                    // We can never emit any (non-empty) downstream changes in the future, if the collection is empty
                    // and the source has reported that it'll never change, so go ahead and complete now.
                    if (_hasPredicateStateCompleted || ((_itemStates.Count is 0) && _suppressEmptyChangeSets))
                    {
                        Monitor.Enter(DownstreamSynchronizationGate, ref hasDownstreamLock);

                        if (hasUpstreamLock)
                        {
                            Monitor.Exit(UpstreamSynchronizationGate);
                            hasUpstreamLock = false;
                        }

                        _downstreamObserver.OnCompleted();
                    }
                }
                finally
                {
                    if (hasUpstreamLock)
                        Monitor.Exit(UpstreamSynchronizationGate);

                    if (hasDownstreamLock)
                        Monitor.Exit(DownstreamSynchronizationGate);
                }
            }

            private void OnSourceNext(IChangeSet<T> upstreamChanges)
            {
                var hasUpstreamLock = false;
                var hasDownstreamLock = false;
                try
                {
                    Monitor.Enter(UpstreamSynchronizationGate, ref hasUpstreamLock);

                    foreach (var change in upstreamChanges)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                PerformAdd(change.Item);
                                break;

                            case ListChangeReason.AddRange:
                                PerformAddRange(change.Range);
                                break;

                            case ListChangeReason.Clear:
                                if (_itemStates.Count is not 0)
                                    PerformClear();
                                break;

                            case ListChangeReason.Moved:
                                if (change.Item.CurrentIndex != change.Item.PreviousIndex)
                                    PerformMove(change.Item);
                                break;

                            case ListChangeReason.Refresh:
                                PerformRefresh(change.Item);
                                break;

                            case ListChangeReason.Remove:
                                PerformRemove(change.Item);
                                break;

                            case ListChangeReason.RemoveRange:
                                PerformRemoveRange(change.Range);
                                break;

                            case ListChangeReason.Replace:
                                PerformReplace(change.Item);
                                break;
                        }
                    }

                    var downstreamChanges = AssembleDownstreamChanges();

                    if ((downstreamChanges.Count is not 0) || !_suppressEmptyChangeSets)
                    {
                        Monitor.Enter(DownstreamSynchronizationGate, ref hasDownstreamLock);

                        if (hasUpstreamLock)
                        {
                            Monitor.Exit(UpstreamSynchronizationGate);
                            hasUpstreamLock = false;
                        }

                        _downstreamObserver.OnNext(downstreamChanges);
                    }
                }
                finally
                {
                    if (hasUpstreamLock)
                        Monitor.Exit(UpstreamSynchronizationGate);

                    if (hasDownstreamLock)
                        Monitor.Exit(DownstreamSynchronizationGate);
                }
            }

            protected readonly struct ItemState
            {
                public required int? FilteredIndex { get; init; }

                public required T Item { get; init; }
            }
        }

        private sealed class CalculateDiffSubscription
            : SubscriptionBase
        {
            public CalculateDiffSubscription(
                    IObserver<IChangeSet<T>> downstreamObserver,
                    Func<TState, T, bool> predicate,
                    bool suppressEmptyChangeSets)
                : base(
                    downstreamObserver: downstreamObserver,
                    predicate: predicate,
                    suppressEmptyChangeSets: suppressEmptyChangeSets)
            {
            }

            protected override void PerformAdd(ItemChange<T> change)
            {
                var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, change.Current);
                var filteredIndex = default(int?);

                if (isIncluded)
                {
                    filteredIndex = 0;
                    for (var i = change.CurrentIndex - 1; i >= 0; --i)
                    {
                        if (ItemStates[i].FilteredIndex is int priorFilteredIndex)
                        {
                            filteredIndex = priorFilteredIndex + 1;
                            break;
                        }
                    }

                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Add,
                        current: change.Current,
                        index: filteredIndex.Value));
                }

                ItemStates.Insert(
                    index: change.CurrentIndex,
                    item: new()
                    {
                        FilteredIndex = filteredIndex,
                        Item = change.Current
                    });

                for (var i = change.CurrentIndex + 1; i < ItemStates.Count; ++i)
                {
                    var otherItemState = ItemStates[i];
                    if (otherItemState.FilteredIndex is int otherFilteredIndex)
                    {
                        ItemStates[i] = otherItemState with
                        {
                            FilteredIndex = otherFilteredIndex + 1
                        };
                    }
                }
            }

            protected override void PerformAddRange(RangeChange<T> change)
            {
                var nextFilteredIndex = 0;
                for (var i = change.Index - 1; i >= 0; --i)
                {
                    if (ItemStates[i].FilteredIndex is int priorFilteredIndex)
                    {
                        nextFilteredIndex = priorFilteredIndex + 1;
                        break;
                    }
                }
                var filteredInsertIndex = nextFilteredIndex;

                foreach (var item in change)
                {
                    var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, item);
                    int? filteredIndex = null;

                    if (isIncluded)
                    {
                        filteredIndex = nextFilteredIndex++;
                        ItemsBuffer.Add(item);
                    }

                    ItemStatesBuffer.Add(new()
                    {
                        FilteredIndex = filteredIndex,
                        Item = item
                    });
                }

                if (ItemStatesBuffer.Count is not 0)
                {
                    ItemStates.InsertRange(change.Index, ItemStatesBuffer);
                    ItemStatesBuffer.Clear();

                    for (var i = change.Index + change.Count; i < ItemStates.Count; ++i)
                    {
                        var otherItemState = ItemStates[i];
                        if (otherItemState.FilteredIndex is int otherFilteredIndex)
                        {
                            ItemStates[i] = otherItemState with
                            {
                                FilteredIndex = otherFilteredIndex + 1
                            };
                        }
                    }

                    if (ItemsBuffer.Count is not 0)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.AddRange,
                            items: ItemsBuffer.ToArray(), // The Change<T> constructor does not safety-copy the collection, we need to do it.
                            index: filteredInsertIndex));
                        ItemsBuffer.Clear();
                    }
                }
            }

            protected override void PerformClear()
            {
                ItemsBuffer.EnsureCapacity(ItemStates.Count);

                foreach (var itemState in ItemStates)
                {
                    if (itemState.FilteredIndex is not null)
                        ItemsBuffer.Add(itemState.Item);
                }
                ItemStates.Clear();

                if (ItemsBuffer.Count is not 0)
                {
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Clear,
                        items: ItemsBuffer.ToArray())); // The Change<T> constructor does not safety-copy the collection, we need to do it.
                    ItemsBuffer.Clear();
                }
            }

            protected override void PerformMove(ItemChange<T> change)
            {
                var itemState = ItemStates[change.PreviousIndex];

                if (itemState.FilteredIndex is int previousFilteredIndex)
                {
                    int currentFilteredIndex;
                    // Determine the filtered index to use for the moved item, after the move, by searching backwards from the target location.
                    // When moving forwards, only search back to the original position of the item, to see if the index needs to change at all,
                    // and if it does, account for the fact that the filtered index of the items in that range will need to be adjusted by -1. to account for the move.
                    if (change.CurrentIndex > change.PreviousIndex)
                    {
                        currentFilteredIndex = previousFilteredIndex;
                        for (var i = change.CurrentIndex; i >= change.PreviousIndex; --i)
                        {
                            if (ItemStates[i].FilteredIndex is int priorFilteredIndex)
                            {
                                currentFilteredIndex = priorFilteredIndex;
                                break;
                            }
                        }
                    }
                    // When moving backwards, search to the beginning of the list, where items' filtered indexes will not be changing.
                    else
                    {
                        currentFilteredIndex = 0;
                        for (var i = change.CurrentIndex - 1; i >= 0; --i)
                        {
                            if (ItemStates[i].FilteredIndex is int priorFilteredIndex)
                            {
                                currentFilteredIndex = priorFilteredIndex + 1;
                                break;
                            }
                        }
                    }

                    if (currentFilteredIndex != previousFilteredIndex)
                    {
                        DownstreamChangesBuffer.Add(new(
                            current: change.Current,
                            currentIndex: currentFilteredIndex,
                            previousIndex: previousFilteredIndex));

                        itemState = itemState with
                        {
                            FilteredIndex = currentFilteredIndex
                        };
                    }
                }

                ItemStates.RemoveAt(change.PreviousIndex);
                ItemStates.Insert(change.CurrentIndex, itemState);

                if (itemState.FilteredIndex is not null)
                {
                    if (change.CurrentIndex < change.PreviousIndex)
                    {
                        for (var i = change.CurrentIndex + 1; i <= change.PreviousIndex; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if (otherItemState.FilteredIndex is int otherFilteredIndex)
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex + 1
                                };
                            }
                        }
                    }
                    else
                    {
                        for (var i = change.PreviousIndex; i < change.CurrentIndex; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if (otherItemState.FilteredIndex is int otherFilteredIndex)
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex - 1
                                };
                            }
                        }
                    }
                }
            }

            protected override void PerformReFilter()
            {
                var nextFilteredIndex = 0;

                for (var unfilteredIndex = 0; unfilteredIndex < ItemStates.Count; ++unfilteredIndex)
                {
                    var itemState = ItemStates[unfilteredIndex];

                    var isIncluded = Predicate.Invoke(LatestPredicateState, itemState.Item);

                    if (itemState.FilteredIndex is int filteredIndex)
                    {
                        if (isIncluded)
                        {
                            if (filteredIndex != nextFilteredIndex)
                            {
                                ItemStates[unfilteredIndex] = itemState with
                                {
                                    FilteredIndex = nextFilteredIndex
                                };
                            }
                            ++nextFilteredIndex;
                        }
                        else
                        {
                            ItemStates[unfilteredIndex] = itemState with
                            {
                                FilteredIndex = null
                            };

                            DownstreamChangesBuffer.Add(new(
                                reason: ListChangeReason.Remove,
                                current: itemState.Item,
                                index: nextFilteredIndex));
                        }
                    }
                    else if (isIncluded)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Add,
                            current: itemState.Item,
                            index: nextFilteredIndex));

                        ItemStates[unfilteredIndex] = new()
                        {
                            FilteredIndex = nextFilteredIndex,
                            Item = itemState.Item
                        };

                        ++nextFilteredIndex;
                    }
                }
            }

            protected override void PerformRefresh(ItemChange<T> change)
            {
                var itemState = ItemStates[change.CurrentIndex];
                var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, change.Current);

                if (itemState.FilteredIndex is int filteredIndex)
                {
                    if (isIncluded)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Refresh,
                            current: change.Current,
                            index: filteredIndex));
                    }
                    else
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Remove,
                            current: change.Current,
                            index: filteredIndex));

                        ItemStates[change.CurrentIndex] = new()
                        {
                            FilteredIndex = null,
                            Item = change.Current
                        };

                        for (var i = change.CurrentIndex + 1; i < ItemStates.Count; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if (otherItemState.FilteredIndex is int otherFilteredIndex)
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex - 1
                                };
                            }
                        }
                    }
                }
                else if (isIncluded)
                {
                    filteredIndex = 0;
                    for (var i = change.CurrentIndex - 1; i >= 0; --i)
                    {
                        if (ItemStates[i].FilteredIndex is int priorFilteredIndex)
                        {
                            filteredIndex = priorFilteredIndex + 1;
                            break;
                        }
                    }

                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Add,
                        current: change.Current,
                        index: filteredIndex));

                    ItemStates[change.CurrentIndex] = new()
                    {
                        FilteredIndex = filteredIndex,
                        Item = change.Current
                    };

                    for (var i = change.CurrentIndex + 1; i < ItemStates.Count; ++i)
                    {
                        var otherItemState = ItemStates[i];
                        if (otherItemState.FilteredIndex is int otherFilteredIndex)
                        {
                            ItemStates[i] = otherItemState with
                            {
                                FilteredIndex = otherFilteredIndex + 1
                            };
                        }
                    }
                }
            }

            protected override void PerformRemove(ItemChange<T> change)
            {
                var itemState = ItemStates[change.CurrentIndex];
                ItemStates.RemoveAt(change.CurrentIndex);

                if (itemState.FilteredIndex is int filteredIndex)
                {
                    for (var i = change.CurrentIndex; i < ItemStates.Count; ++i)
                    {
                        var otherItemState = ItemStates[i];
                        if (otherItemState.FilteredIndex is int otherFilteredIndex)
                        {
                            ItemStates[i] = otherItemState with
                            {
                                FilteredIndex = otherFilteredIndex - 1
                            };
                        }
                    }

                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Remove,
                        current: change.Current,
                        index: filteredIndex));
                }
            }

            protected override void PerformRemoveRange(RangeChange<T> change)
            {
                ItemsBuffer.EnsureCapacity(change.Count);
                var filteredRangeIndex = -1;

                for (var i = change.Index; i < change.Index + change.Count; ++i)
                {
                    var itemState = ItemStates[i];
                    if (itemState.FilteredIndex is int filteredIndex)
                    {
                        if (filteredRangeIndex is -1)
                            filteredRangeIndex = filteredIndex;

                        ItemsBuffer.Add(itemState.Item);
                    }
                }

                ItemStates.RemoveRange(change.Index, change.Count);

                if (ItemsBuffer.Count is not 0)
                {
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.RemoveRange,
                        items: ItemsBuffer.ToArray(), // The Change<T> constructor does not clone the collection, we need to do it.
                        index: filteredRangeIndex));

                    for (var i = change.Index; i < ItemStates.Count; ++i)
                    {
                        var otherItemState = ItemStates[i];
                        if (otherItemState.FilteredIndex is int otherFilteredIndex)
                        {
                            ItemStates[i] = otherItemState with
                            {
                                FilteredIndex = otherFilteredIndex - ItemsBuffer.Count
                            };
                        }
                    }

                    ItemsBuffer.Clear();
                }
            }

            protected override void PerformReplace(ItemChange<T> change)
            {
                var itemState = ItemStates[change.CurrentIndex];
                var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, change.Current);

                if (itemState.FilteredIndex is int filteredIndex)
                {
                    if (isIncluded)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Replace,
                            current: change.Current,
                            previous: change.Previous,
                            currentIndex: filteredIndex,
                            previousIndex: filteredIndex));

                        ItemStates[change.CurrentIndex] = itemState with
                        {
                            Item = change.Current
                        };
                    }
                    else
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Remove,
                            current: change.Previous.Value,
                            index: filteredIndex));

                        ItemStates[change.CurrentIndex] = new()
                        {
                            FilteredIndex = null,
                            Item = change.Current
                        };

                        for (var i = change.CurrentIndex + 1; i < ItemStates.Count; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if (otherItemState.FilteredIndex is int otherFilteredIndex)
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex - 1
                                };
                            }
                        }
                    }
                }
                else
                {
                    if (isIncluded)
                    {
                        filteredIndex = 0;
                        for (var i = change.CurrentIndex - 1; i >= 0; --i)
                        {
                            if (ItemStates[i].FilteredIndex is int priorFilteredIndex)
                            {
                                filteredIndex = priorFilteredIndex + 1;
                                break;
                            }
                        }

                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Add,
                            current: change.Current,
                            index: filteredIndex));

                        ItemStates[change.CurrentIndex] = new()
                        {
                            FilteredIndex = filteredIndex,
                            Item = change.Current
                        };

                        for (var i = change.CurrentIndex + 1; i < ItemStates.Count; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if (otherItemState.FilteredIndex is int otherFilteredIndex)
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex + 1
                                };
                            }
                        }
                    }
                    else
                    {
                        ItemStates[change.CurrentIndex] = itemState with
                        {
                            Item = change.Current
                        };
                    }
                }
            }
        }

        private sealed class ClearAndReplaceSubscription
            : SubscriptionBase
        {
            private int _filteredCount;

            public ClearAndReplaceSubscription(
                    IObserver<IChangeSet<T>> downstreamObserver,
                    Func<TState, T, bool> predicate,
                    bool suppressEmptyChangeSets)
                : base(
                    downstreamObserver: downstreamObserver,
                    predicate: predicate,
                    suppressEmptyChangeSets: suppressEmptyChangeSets)
            {
            }

            protected override void PerformAdd(ItemChange<T> change)
            {
                var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, change.Current);
                var filteredIndex = default(int?);

                if (isIncluded)
                {
                    filteredIndex = _filteredCount++;
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Add,
                        current: change.Current,
                        index: filteredIndex.Value));
                }

                ItemStates.Insert(
                    index: change.CurrentIndex,
                    item: new()
                    {
                        FilteredIndex = filteredIndex,
                        Item = change.Current
                    });
            }

            protected override void PerformAddRange(RangeChange<T> change)
            {
                var priorFilteredCount = _filteredCount;

                foreach (var item in change)
                {
                    var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, item);
                    int? filteredIndex = null;

                    if (isIncluded)
                    {
                        filteredIndex = _filteredCount++;
                        ItemsBuffer.Add(item);
                    }

                    ItemStatesBuffer.Add(new()
                    {
                        FilteredIndex = filteredIndex,
                        Item = item
                    });
                }

                if (ItemStatesBuffer.Count is not 0)
                {
                    ItemStates.InsertRange(change.Index, ItemStatesBuffer);
                    ItemStatesBuffer.Clear();

                    if (ItemsBuffer.Count is not 0)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.AddRange,
                            items: ItemsBuffer.ToArray(), // The Change<T> constructor does not clone the collection, we need to do it.
                            index: priorFilteredCount));
                        ItemsBuffer.Clear();
                    }
                }
            }

            protected override void PerformClear()
            {
                // Not using ItemsBuffer, because we already know the exact size we need, so we can allocate a fresh one and use it directly.
                var itemsBuffer = new T[_filteredCount];

                foreach (var itemState in ItemStates)
                {
                    if (itemState.FilteredIndex is int filteredIndex)
                        itemsBuffer[filteredIndex] = itemState.Item;
                }
                ItemStates.Clear();
                _filteredCount = 0;

                if (itemsBuffer.Length is not 0)
                {
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Clear,
                        items: itemsBuffer));
                }
            }

            protected override void PerformMove(ItemChange<T> change)
            {
                // We're not supporting propagation of move changes, but we do still need to process them, to keep ItemStates correct.
                var itemState = ItemStates[change.PreviousIndex];
                ItemStates.RemoveAt(change.PreviousIndex);
                ItemStates.Insert(change.CurrentIndex, itemState);
            }

            protected override void PerformReFilter()
            {
                var nextFilteredIndex = 0;
                var clearedItems = (_filteredCount is 0)
                    ? Array.Empty<T>()
                    : new T[_filteredCount];

                for (var unfilteredIndex = 0; unfilteredIndex < ItemStates.Count; ++unfilteredIndex)
                {
                    var itemState = ItemStates[unfilteredIndex];

                    var isIncluded = Predicate.Invoke(LatestPredicateState, itemState.Item);

                    if (itemState.FilteredIndex is int filteredIndex)
                    {
                        clearedItems[filteredIndex] = itemState.Item;

                        if (isIncluded)
                        {
                            ItemsBuffer.Add(itemState.Item);

                            if (filteredIndex != nextFilteredIndex)
                            {
                                ItemStates[unfilteredIndex] = itemState with
                                {
                                    FilteredIndex = nextFilteredIndex
                                };
                            }

                            ++nextFilteredIndex;
                        }
                        else
                        {
                            --_filteredCount;
                            ItemStates[unfilteredIndex] = itemState with
                            {
                                FilteredIndex = null
                            };
                        }
                    }
                    else if (isIncluded)
                    {
                        ++_filteredCount;
                        ItemStates[unfilteredIndex] = itemState with
                        {
                            FilteredIndex = nextFilteredIndex
                        };

                        ItemsBuffer.Add(itemState.Item);

                        ++nextFilteredIndex;
                    }
                }

                if (clearedItems.Length is not 0)
                {
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Clear,
                        items: clearedItems));
                }

                if (ItemsBuffer.Count is not 0)
                {
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.AddRange,
                        items: ItemsBuffer.ToArray(), // The Change<T> constructor does not safety-copy the collection, we need to do it.
                        index: 0));
                    ItemsBuffer.Clear();
                }
            }

            protected override void PerformRefresh(ItemChange<T> change)
            {
                var itemState = ItemStates[change.CurrentIndex];
                var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, change.Current);

                if (itemState.FilteredIndex is int filteredIndex)
                {
                    if (isIncluded)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Refresh,
                            current: change.Current,
                            index: filteredIndex));
                    }
                    else
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Remove,
                            current: change.Current,
                            index: filteredIndex));

                        ItemStates[change.CurrentIndex] = new()
                        {
                            FilteredIndex = null,
                            Item = change.Current
                        };
                        --_filteredCount;

                        for (var i = 0; i < ItemStates.Count; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if ((otherItemState.FilteredIndex is int otherFilteredIndex) && (otherItemState.FilteredIndex > filteredIndex))
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex - 1
                                };
                            }
                        }
                    }
                }
                else if (isIncluded)
                {
                    filteredIndex = _filteredCount++;
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Add,
                        current: change.Current,
                        index: filteredIndex));

                    ItemStates[change.CurrentIndex] = new()
                    {
                        FilteredIndex = filteredIndex,
                        Item = change.Current
                    };
                }
            }

            protected override void PerformRemove(ItemChange<T> change)
            {
                var itemState = ItemStates[change.CurrentIndex];
                ItemStates.RemoveAt(change.CurrentIndex);

                if (itemState.FilteredIndex is int filteredIndex)
                {
                    --_filteredCount;

                    for (var i = 0; i < ItemStates.Count; ++i)
                    {
                        var otherItemState = ItemStates[i];
                        if ((otherItemState.FilteredIndex is int otherFilteredIndex) && (otherItemState.FilteredIndex > filteredIndex))
                        {
                            ItemStates[i] = otherItemState with
                            {
                                FilteredIndex = --otherFilteredIndex
                            };
                        }
                    }

                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Remove,
                        current: change.Current,
                        index: filteredIndex));
                }
            }

            protected override void PerformRemoveRange(RangeChange<T> change)
            {
                for (var index = change.Index; index < change.Index + change.Count; ++index)
                {
                    var itemState = ItemStates[index];
                    if (itemState.FilteredIndex is int filteredIndex)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Remove,
                            current: itemState.Item,
                            index: filteredIndex));

                        for (var i = 0; i < ItemStates.Count; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if ((otherItemState.FilteredIndex is int otherFilteredIndex) && (otherItemState.FilteredIndex > filteredIndex))
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex - 1
                                };
                            }
                        }
                        --_filteredCount;
                    }
                }

                ItemStates.RemoveRange(change.Index, change.Count);
            }

            protected override void PerformReplace(ItemChange<T> change)
            {
                var itemState = ItemStates[change.CurrentIndex];
                var isIncluded = IsLatestPredicateStateValid && Predicate.Invoke(LatestPredicateState, change.Current);

                if (itemState.FilteredIndex is int filteredIndex)
                {
                    if (isIncluded)
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Replace,
                            current: change.Current,
                            previous: change.Previous,
                            currentIndex: filteredIndex,
                            previousIndex: filteredIndex));

                        ItemStates[change.CurrentIndex] = itemState with
                        {
                            Item = change.Current
                        };
                    }
                    else
                    {
                        DownstreamChangesBuffer.Add(new(
                            reason: ListChangeReason.Remove,
                            current: change.Previous.Value,
                            index: filteredIndex));

                        ItemStates[change.CurrentIndex] = new()
                        {
                            FilteredIndex = null,
                            Item = change.Current
                        };
                        --_filteredCount;

                        for (var i = 0; i < ItemStates.Count; ++i)
                        {
                            var otherItemState = ItemStates[i];
                            if ((otherItemState.FilteredIndex is int otherFilteredIndex) && (otherItemState.FilteredIndex > filteredIndex))
                            {
                                ItemStates[i] = otherItemState with
                                {
                                    FilteredIndex = otherFilteredIndex - 1
                                };
                            }
                        }
                    }
                }
                else if (isIncluded)
                {
                    filteredIndex = _filteredCount++;
                    DownstreamChangesBuffer.Add(new(
                        reason: ListChangeReason.Add,
                        current: change.Current,
                        index: filteredIndex));

                    ItemStates[change.CurrentIndex] = new()
                    {
                        FilteredIndex = filteredIndex,
                        Item = change.Current
                    };
                }
                else
                {
                    ItemStates[change.CurrentIndex] = itemState with
                    {
                        Item = change.Current
                    };
                }
            }
        }
    }
}
