// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the Filter class.
/// </summary>
internal static partial class Filter
{
/// <summary>
/// Provides members for the Dynamic class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TState">The type of the TState value.</typeparam>
public static class Dynamic<TObject, TKey, TState>
        where TObject : notnull
        where TKey : notnull
    {
        /// <summary>
        /// Executes the Create operation.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="predicateState">The predicateState value.</param>
        /// <param name="predicate">The predicate value.</param>
        /// <param name="reapplyFilter">The reapplyFilter value.</param>
        /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
        /// <returns>The result of the operation.</returns>
        public static IObservable<IChangeSet<TObject, TKey>> Create(
            IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<TState> predicateState,
            Func<TState, TObject, bool> predicate,
            IObservable<Unit> reapplyFilter,
            bool suppressEmptyChangeSets)
        {
            ArgumentExceptionHelper.ThrowIfNull(source);
            ArgumentExceptionHelper.ThrowIfNull(predicateState);
            ArgumentExceptionHelper.ThrowIfNull(predicate);
            ArgumentExceptionHelper.ThrowIfNull(reapplyFilter);

            return Observable.Create<IChangeSet<TObject, TKey>>(downstreamObserver => new Subscription(
                downstreamObserver: downstreamObserver,
                predicate: predicate,
                predicateState: predicateState,
                reapplyFilter: reapplyFilter,
                source: source,
                suppressEmptyChangeSets: suppressEmptyChangeSets));
        }

/// <summary>
/// Provides members for the Subscription class.
/// </summary>
private sealed class Subscription
            : IDisposable
        {
            /// <summary>
            /// The _downstreamChangesBuffer field.
            /// </summary>
            private readonly List<Change<TObject, TKey>> _downstreamChangesBuffer;

            /// <summary>
            /// The _downstreamObserver field.
            /// </summary>
            private readonly IObserver<IChangeSet<TObject, TKey>> _downstreamObserver;

            /// <summary>
            /// The _itemStatesByKey field.
            /// </summary>
            private readonly Dictionary<TKey, ItemState> _itemStatesByKey;

            /// <summary>
            /// The _predicate field.
            /// </summary>
            private readonly Func<TState, TObject, bool> _predicate;

            /// <summary>
            /// The _predicateStateSubscription field.
            /// </summary>
            private readonly IDisposable? _predicateStateSubscription;

            /// <summary>
            /// The _reapplyFilterSubscription field.
            /// </summary>
            private readonly IDisposable? _reapplyFilterSubscription;

            /// <summary>
            /// The _sourceSubscription field.
            /// </summary>
            private readonly IDisposable? _sourceSubscription;

            /// <summary>
            /// The _suppressEmptyChangeSets field.
            /// </summary>
            private readonly bool _suppressEmptyChangeSets;

            /// <summary>
            /// The _downstreamGate field.
            /// </summary>
            private readonly Lock _downstreamGate = new();

            /// <summary>
            /// The _upstreamGate field.
            /// </summary>
            private readonly Lock _upstreamGate = new();

            /// <summary>
            /// The _hasInitialized field.
            /// </summary>
            private bool _hasInitialized;

            /// <summary>
            /// The _hasPredicateStateCompleted field.
            /// </summary>
            private bool _hasPredicateStateCompleted;

            /// <summary>
            /// The _hasReapplyFilterCompleted field.
            /// </summary>
            private bool _hasReapplyFilterCompleted;

            /// <summary>
            /// The _hasSourceCompleted field.
            /// </summary>
            private bool _hasSourceCompleted;

            /// <summary>
            /// The _isLatestPredicateStateValid field.
            /// </summary>
            private bool _isLatestPredicateStateValid;

            /// <summary>
            /// The _latestPredicateState field.
            /// </summary>
            private TState _latestPredicateState;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscription"/> class.
            /// </summary>
            /// <param name="downstreamObserver">The downstreamObserver value.</param>
            /// <param name="predicate">The predicate value.</param>
            /// <param name="predicateState">The predicateState value.</param>
            /// <param name="reapplyFilter">The reapplyFilter value.</param>
            /// <param name="source">The source value.</param>
            /// <param name="suppressEmptyChangeSets">The suppressEmptyChangeSets value.</param>
            public Subscription(
                IObserver<IChangeSet<TObject, TKey>> downstreamObserver,
                Func<TState, TObject, bool> predicate,
                IObservable<TState> predicateState,
                IObservable<Unit> reapplyFilter,
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

                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _predicateStateSubscription = predicateState
                    .SubscribeSafe(
                        onNext: OnPredicateStateNext,
                        onError: onError,
                        onCompleted: OnPredicateStateCompleted);

                _reapplyFilterSubscription = reapplyFilter
                    .SubscribeSafe(
                        onNext: OnReapplyFilterNext,
                        onError: onError,
                        onCompleted: OnReapplyFilterCompleted);

                _sourceSubscription = source
                    .SubscribeSafe(
                        onNext: OnSourceNext,
                        onError: onError,
                        onCompleted: OnSourceCompleted);

                _hasInitialized = true;

                // We withhold completions triggered by the other upstreams until after the source stream has a chance to publish an initial changeset.
                // If that happens, we need to publish the completion now.
                var needToComplete = _suppressEmptyChangeSets
                    && _hasPredicateStateCompleted
                    && !_isLatestPredicateStateValid;

                if (needToComplete)
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnCompleted();
                }
            }

            /// <summary>
            /// Executes the Dispose operation.
            /// </summary>
            public void Dispose()
            {
                _predicateStateSubscription?.Dispose();
                _reapplyFilterSubscription?.Dispose();
                _sourceSubscription?.Dispose();
            }

            /// <summary>
            /// Gets the DownstreamSynchronizationGate value.
            /// </summary>
            private Lock DownstreamSynchronizationGate
                => _downstreamGate;

            /// <summary>
            /// Gets the UpstreamSynchronizationGate value.
            /// </summary>
            private Lock UpstreamSynchronizationGate
                => _upstreamGate;

            /// <summary>
            /// Executes the AssembleDownstreamChanges operation.
            /// </summary>
            /// <returns>The result of the operation.</returns>
            private ChangeSet<TObject, TKey> AssembleDownstreamChanges()
            {
                if (_downstreamChangesBuffer.Count is 0)
                    return ChangeSet<TObject, TKey>.Empty;

                var downstreamChanges = new ChangeSet<TObject, TKey>(_downstreamChangesBuffer);
                _downstreamChangesBuffer.Clear();

                return downstreamChanges;
            }

            /// <summary>
            /// Executes the OnError operation.
            /// </summary>
            /// <param name="error">The error value.</param>
            private void OnError(Exception error)
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _predicateStateSubscription?.Dispose();
                _sourceSubscription?.Dispose();

                @lock.SwapTo(DownstreamSynchronizationGate);

                _downstreamObserver.OnError(error);
            }

            /// <summary>
            /// Executes the OnPredicateStateCompleted operation.
            /// </summary>
            private void OnPredicateStateCompleted()
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _hasPredicateStateCompleted = true;

                // If we didn't get at least one predicateState value, we can't ever emit any (non-empty) downstream changesets,
                // no matter how many items come through from source, so just go ahead and complete now.
                if (_hasInitialized
                    && ((_hasReapplyFilterCompleted && _hasSourceCompleted)
                        || (!_isLatestPredicateStateValid && _suppressEmptyChangeSets)))
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnCompleted();
                }
            }

            /// <summary>
            /// Executes the OnPredicateStateNext operation.
            /// </summary>
            /// <param name="predicateState">The predicateState value.</param>
            private void OnPredicateStateNext(TState predicateState)
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _latestPredicateState = predicateState;
                _isLatestPredicateStateValid = true;

                ReFilter(predicateState);

                var downstreamChanges = AssembleDownstreamChanges();
                if (((downstreamChanges.Count is not 0) || !_suppressEmptyChangeSets) && _hasInitialized)
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnNext(downstreamChanges);
                }
            }

            /// <summary>
            /// Executes the OnReapplyFilterCompleted operation.
            /// </summary>
            private void OnReapplyFilterCompleted()
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _hasReapplyFilterCompleted = true;

                // If the other two sources have also completed, there's no chance of us ever needing to emit further changesets.
                if (_hasPredicateStateCompleted && _hasSourceCompleted)
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnCompleted();
                }
            }

            /// <summary>
            /// Executes the OnReapplyFilterNext operation.
            /// </summary>
            /// <param name="value">The value value.</param>
            private void OnReapplyFilterNext(Unit value)
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                if (_isLatestPredicateStateValid)
                    ReFilter(_latestPredicateState);

                var downstreamChanges = AssembleDownstreamChanges();
                if (((downstreamChanges.Count is not 0) || !_suppressEmptyChangeSets) && _hasInitialized)
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnNext(downstreamChanges);
                }
            }

            /// <summary>
            /// Executes the OnSourceCompleted operation.
            /// </summary>
            private void OnSourceCompleted()
            {
                using var @lock = SwappableLock.CreateAndEnter(UpstreamSynchronizationGate);

                _hasSourceCompleted = true;

                // We can never emit any (non-empty) downstream changes in the future, if the collection is empty
                // and the source has reported that it'll never change, so go ahead and complete now.
                if ((_hasPredicateStateCompleted && _hasReapplyFilterCompleted)
                    || (_suppressEmptyChangeSets
                       && ((_itemStatesByKey.Count is 0)
                           || (_hasPredicateStateCompleted && !_isLatestPredicateStateValid))))
                {
                    @lock.SwapTo(DownstreamSynchronizationGate);

                    _downstreamObserver.OnCompleted();
                }
            }

            /// <summary>
            /// Executes the OnSourceNext operation.
            /// </summary>
            /// <param name="upstreamChanges">The upstreamChanges value.</param>
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

            /// <summary>
            /// Executes the ReFilter operation.
            /// </summary>
            /// <param name="predicateState">The predicateState value.</param>
            private void ReFilter(TState predicateState)
            {
                #if SUPPORTS_DICTIONARY_MUTATION_DURING_ENUMERATION
                foreach (var key in _itemStatesByKey.Keys)
                #else
                foreach (var key in _itemStatesByKey.Keys.ToArray())
                #endif
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
            }
        }

/// <summary>
/// Represents the ItemState value.
/// </summary>
private readonly struct ItemState
        {
            /// <summary>
            /// Gets or sets the IsIncluded value.
            /// </summary>
            public required bool IsIncluded { get; init; }

            /// <summary>
            /// Gets or sets the Item value.
            /// </summary>
            public required TObject Item { get; init; }
        }
    }
}
