// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal static partial class Filter
{
    public static class WithPredicateState<TObject, TKey, TState>
        where TObject : notnull
        where TKey : notnull
    {
        public static IObservable<IChangeSet<TObject, TKey>> Create(
            IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<TState> predicateState,
            Func<TState, TObject, bool> predicate,
            bool suppressEmptyChangeSets)
        {
            source.ThrowArgumentNullExceptionIfNull(nameof(source));
            predicateState.ThrowArgumentNullExceptionIfNull(nameof(predicateState));
            predicate.ThrowArgumentNullExceptionIfNull(nameof(predicate));

            return Observable.Create<IChangeSet<TObject, TKey>>(downstreamObserver => new Subscription(
                downstreamObserver: downstreamObserver,
                predicate: predicate,
                predicateState: predicateState,
                source: source,
                suppressEmptyChangeSets: suppressEmptyChangeSets));
        }

        private sealed class Subscription
            : IDisposable
        {
            private readonly List<Change<TObject, TKey>> _downstreamChangesBuffer;
            private readonly IObserver<IChangeSet<TObject, TKey>> _downstreamObserver;
            private readonly Dictionary<TKey, ItemState> _itemStatesByKey;
            private readonly Func<TState, TObject, bool> _predicate;
            private readonly IDisposable? _predicateStateSubscription;
            private readonly IDisposable? _sourceSubscription;
            private readonly bool _suppressEmptyChangeSets;

            private bool _hasPredicateStateCompleted;
            private bool _hasSourceCompleted;
            private bool _isLatestPredicateStateValid;
            private TState _latestPredicateState;

            public Subscription(
                IObserver<IChangeSet<TObject, TKey>> downstreamObserver,
                Func<TState, TObject, bool> predicate,
                IObservable<TState> predicateState,
                IObservable<IChangeSet<TObject, TKey>> source,
                bool suppressEmptyChangeSets)
            {
                _downstreamObserver = downstreamObserver;
                _predicate = predicate;
                _suppressEmptyChangeSets = suppressEmptyChangeSets;

                _downstreamChangesBuffer = [];
                _itemStatesByKey = [];

                _latestPredicateState = default!;

                var onError = new Action<Exception>(OnError);

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

            private object DownstreamSynchronizationGate
                => _downstreamChangesBuffer;

            private object UpstreamSynchronizationGate
                => _itemStatesByKey;

            private ChangeSet<TObject, TKey> AssembleDownstreamChanges()
            {
                if (_downstreamChangesBuffer.Count is 0)
                    return ChangeSet<TObject, TKey>.Empty;

                var downstreamChanges = new ChangeSet<TObject, TKey>(_downstreamChangesBuffer);
                _downstreamChangesBuffer.Clear();

                return downstreamChanges;
            }

            private void OnError(Exception error)
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _predicateStateSubscription?.Dispose();
                _sourceSubscription?.Dispose();

                @lock.SwapTo(DownstreamSynchronizationGate);

                _downstreamObserver.OnError(error);
            }

            private void OnPredicateStateCompleted()
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _hasPredicateStateCompleted = true;

                // If we didn't get at least one predicateState value, we can't ever emit any (non-empty) downstream changesets,
                // no matter how many items come through from source, so just go ahead and complete now.
                if (_hasSourceCompleted || (!_isLatestPredicateStateValid && _suppressEmptyChangeSets))
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnCompleted();
                }
            }

            private void OnPredicateStateNext(TState predicateState)
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _latestPredicateState = predicateState;
                _isLatestPredicateStateValid = true;

                foreach (var key in _itemStatesByKey.Keys)
                {
                    var itemState = _itemStatesByKey[key];

                    var isIncluded = _predicate.Invoke(predicateState, itemState.Item);

                    if (isIncluded && !itemState.IsIncluded)
                    {
                        _downstreamChangesBuffer.Add(new(
                            reason: ChangeReason.Add,
                            key: key,
                            current: itemState.Item));
                    }
                    else if (!isIncluded && itemState.IsIncluded)
                    {
                        _downstreamChangesBuffer.Add(new(
                            reason: ChangeReason.Remove,
                            key: key,
                            current: itemState.Item));
                    }

                    _itemStatesByKey[key] = new()
                    {
                        IsIncluded = isIncluded,
                        Item = itemState.Item
                    };
                }

                var downstreamChanges = AssembleDownstreamChanges();
                if ((downstreamChanges.Count is not 0) || !_suppressEmptyChangeSets)
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnNext(downstreamChanges);
                }
            }

            private void OnSourceCompleted()
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _hasSourceCompleted = true;

                // We can never emit any (non-empty) downstream changes in the future, if the collection is empty
                // and the source has reported that it'll never change, so go ahead and complete now.
                if (_hasPredicateStateCompleted || ((_itemStatesByKey.Count is 0) && _suppressEmptyChangeSets))
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnCompleted();
                }
            }

            private void OnSourceNext(IChangeSet<TObject, TKey> upstreamChanges)
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                foreach (var change in upstreamChanges.ToConcreteType())
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                var isIncluded = _isLatestPredicateStateValid && _predicate.Invoke(_latestPredicateState, change.Current);

                                _itemStatesByKey.Add(
                                    key: change.Key,
                                    value: new()
                                    {
                                        IsIncluded = isIncluded,
                                        Item = change.Current
                                    });

                                if (isIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Add,
                                        key: change.Key,
                                        current: change.Current));
                                }
                            }
                            break;

                        // Intentionally not supporting Moved changes, too much work to try and track indexes.

                        case ChangeReason.Refresh:
                            {
                                var itemState = _itemStatesByKey[change.Key];

                                var isIncluded = _isLatestPredicateStateValid && _predicate.Invoke(_latestPredicateState, itemState.Item);

                                if (isIncluded && itemState.IsIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Refresh,
                                        key: change.Key,
                                        current: itemState.Item));
                                }
                                else if (isIncluded && !itemState.IsIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Add,
                                        key: change.Key,
                                        current: itemState.Item));
                                }
                                else if (!isIncluded && itemState.IsIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Remove,
                                        key: change.Key,
                                        current: itemState.Item));
                                }

                                _itemStatesByKey[change.Key] = new()
                                {
                                    IsIncluded = isIncluded,
                                    Item = itemState.Item
                                };
                            }
                            break;

                        case ChangeReason.Remove:
                            {
                                var itemState = _itemStatesByKey[change.Key];

                                _itemStatesByKey.Remove(change.Key);

                                if (itemState.IsIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Remove,
                                        key: change.Key,
                                        current: itemState.Item));
                                }
                            }
                            break;

                        case ChangeReason.Update:
                            {
                                var itemState = _itemStatesByKey[change.Key];

                                var isIncluded = _isLatestPredicateStateValid && _predicate.Invoke(_latestPredicateState, change.Current);

                                if (isIncluded && itemState.IsIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Update,
                                        key: change.Key,
                                        current: change.Current,
                                        previous: itemState.Item));
                                }
                                else if (isIncluded && !itemState.IsIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Add,
                                        key: change.Key,
                                        current: change.Current));
                                }
                                else if (!isIncluded && itemState.IsIncluded)
                                {
                                    _downstreamChangesBuffer.Add(new(
                                        reason: ChangeReason.Remove,
                                        key: change.Key,
                                        current: itemState.Item));
                                }

                                _itemStatesByKey[change.Key] = new()
                                {
                                    IsIncluded = isIncluded,
                                    Item = change.Current
                                };
                            }
                            break;
                    }
                }

                var downstreamChanges = AssembleDownstreamChanges();
                if ((downstreamChanges.Count is not 0) || !_suppressEmptyChangeSets)
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnNext(downstreamChanges);
                }
            }
        }

        private readonly struct ItemState
        {
            public required bool IsIncluded { get; init; }

            public required TObject Item { get; init; }
        }
    }
}
