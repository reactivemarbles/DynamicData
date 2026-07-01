// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the FullJoin class.
/// </summary>
/// <typeparam name="TLeft">The type of the TLeft value.</typeparam>
/// <typeparam name="TLeftKey">The type of the TLeftKey value.</typeparam>
/// <typeparam name="TRight">The type of the TRight value.</typeparam>
/// <typeparam name="TRightKey">The type of the TRightKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <param name="left">The left value.</param>
/// <param name="right">The right value.</param>
/// <param name="rightKeySelector">The rightKeySelector value.</param>
/// <param name="resultSelector">The resultSelector value.</param>
internal sealed class FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, ReactiveUI.Primitives.Optional<TLeft>, ReactiveUI.Primitives.Optional<TRight>, TDestination> resultSelector)
    where TLeft : notnull
    where TLeftKey : notnull
    where TRight : notnull
    where TRightKey : notnull
    where TDestination : notnull
{
    /// <summary>
    /// The _left field.
    /// </summary>
    private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left = left ?? throw new ArgumentNullException(nameof(left));

    /// <summary>
    /// The _resultSelector field.
    /// </summary>
    private readonly Func<TLeftKey, ReactiveUI.Primitives.Optional<TLeft>, ReactiveUI.Primitives.Optional<TRight>, TDestination> _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));

    /// <summary>
    /// The _right field.
    /// </summary>
    private readonly IObservable<IChangeSet<TRight, TRightKey>> _right = right ?? throw new ArgumentNullException(nameof(right));

    /// <summary>
    /// The _rightKeySelector field.
    /// </summary>
    private readonly Func<TRight, TLeftKey> _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TLeftKey>> Run() => Observable.Create<IChangeSet<TDestination, TLeftKey>>(
            observer =>
            {
                var locker = InternalEx.NewLock();
                var queue = new SharedDeliveryQueue(locker);

                // create local backing stores
                var leftCache = _left.SynchronizeSafe(queue).AsObservableCache();
                var rightCache = _right.SynchronizeSafe(queue).ChangeKey(_rightKeySelector).AsObservableCache();

                // joined is the final cache
                var joinedCache = new ChangeAwareCache<TDestination, TLeftKey>();

                var leftLoader = leftCache.Connect().Select(changes =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        var leftCurrent = change.Current;
                        var rightLookup = rightCache.Lookup(change.Key);

                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                joinedCache.AddOrUpdate(_resultSelector(change.Key, leftCurrent, rightLookup), change.Key);
                                break;

                            case ChangeReason.Remove:

                                if (!rightLookup.HasValue)
                                {
                                    // remove from result because there is no left and no rights
                                    joinedCache.Remove(change.Key);
                                }
                                else
                                {
                                    // update with no left value
                                    joinedCache.AddOrUpdate(_resultSelector(change.Key, ReactiveUI.Primitives.Optional<TLeft>.None, rightLookup), change.Key);
                                }

                                break;

                            case ChangeReason.Refresh:
                                // propagate upstream
                                joinedCache.Refresh(change.Key);
                                break;
                        }
                    }

                    return joinedCache.CaptureChanges();
                });

                var rightLoader = rightCache.Connect().Select(changes =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        var right = change.Current;
                        var left = leftCache.Lookup(change.Key);

                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                {
                                    joinedCache.AddOrUpdate(_resultSelector(change.Key, left, right), change.Key);
                                }

                                break;

                            case ChangeReason.Remove:
                                {
                                    if (!left.HasValue)
                                    {
                                        // remove from result because there is no left and no rights
                                        joinedCache.Remove(change.Key);
                                    }
                                    else
                                    {
                                        // update with no right value
                                        joinedCache.AddOrUpdate(_resultSelector(change.Key, left, ReactiveUI.Primitives.Optional<TRight>.None), change.Key);
                                    }
                                }

                                break;

                            case ChangeReason.Refresh:
                                // propagate upstream
                                joinedCache.Refresh(change.Key);
                                break;
                        }
                    }

                    return joinedCache.CaptureChanges();
                });

                lock (locker)
                {
                    return new CompositeDisposable(leftLoader.Merge(rightLoader).SubscribeSafe(observer), leftCache, rightCache, queue);
                }
            });
}
