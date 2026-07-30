// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the LeftJoin class.
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
internal sealed class LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, ReactiveUI.Primitives.Optional<TRight>, TDestination> resultSelector)
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
    private readonly Func<TLeftKey, TLeft, ReactiveUI.Primitives.Optional<TRight>, TDestination> _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));

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
                var leftShare = _left.SynchronizeSafe(queue).Publish();
                var leftCache = leftShare.AsObservableCache();

                var rightShare = _right.SynchronizeSafe(queue).Publish();
                var rightCache = rightShare.AsObservableCache();
                var rightForeignCache = rightShare.ChangeKey(_rightKeySelector).AsObservableCache();

                var rightForeignKeysByKey = new Dictionary<TRightKey, TLeftKey>();

                // joined is the final cache
                var joined = new ChangeAwareCache<TDestination, TLeftKey>();

                var hasInitialized = false;

                var leftLoader = leftCache.Connect().Select(
                    changes =>
                    {
                        foreach (var change in changes.ToConcreteType())
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                case ChangeReason.Update:
                                    // Update with left (and right if it is presents)
                                    var leftCurrent = change.Current;
                                    var rightLookup = rightForeignCache.Lookup(change.Key);
                                    joined.AddOrUpdate(_resultSelector(change.Key, leftCurrent, rightLookup), change.Key);
                                    break;

                                case ChangeReason.Remove:
                                    // remove from result because a left value is expected
                                    joined.Remove(change.Key);
                                    break;

                                case ChangeReason.Refresh:
                                    // propagate upstream
                                    joined.Refresh(change.Key);
                                    break;
                            }
                        }

                        return joined.CaptureChanges();
                    });

                var rightLoader = rightCache.Connect()
                    .Select(changes =>
                    {
                        foreach (var change in changes.ToConcreteType())
                        {
                            var right = change.Current;
                            var foreignKey = _rightKeySelector.Invoke(change.Current);
                            var left = leftCache.Lookup(foreignKey);

                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                case ChangeReason.Update:
                                    {
                                        if (rightForeignKeysByKey.TryGetValue(change.Key, out var priorForeignKey)
                                            && !EqualityComparer<TLeftKey>.Default.Equals(foreignKey, priorForeignKey))
                                        {
                                            var priorLeft = leftCache.Lookup(priorForeignKey);
                                            if (priorLeft.HasValue)
                                                joined.AddOrUpdate(_resultSelector(priorForeignKey, priorLeft.Value, ReactiveUI.Primitives.Optional<TRight>.None), priorForeignKey);
                                        }

                                        if (left.HasValue)
                                            joined.AddOrUpdate(_resultSelector(foreignKey, left.Value, right), foreignKey);

                                        rightForeignKeysByKey[change.Key] = foreignKey;
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    if (left.HasValue)
                                        joined.AddOrUpdate(_resultSelector(foreignKey, left.Value, ReactiveUI.Primitives.Optional<TRight>.None), foreignKey);

                                    rightForeignKeysByKey.Remove(change.Key);

                                    break;

                                case ChangeReason.Refresh:
                                    {
                                        if (rightForeignKeysByKey.TryGetValue(change.Key, out var priorForeignKey)
                                            && !EqualityComparer<TLeftKey>.Default.Equals(foreignKey, priorForeignKey))
                                        {
                                            var priorLeft = leftCache.Lookup(priorForeignKey);
                                            if (priorLeft.HasValue)
                                                joined.AddOrUpdate(_resultSelector(priorForeignKey, priorLeft.Value, ReactiveUI.Primitives.Optional<TRight>.None), priorForeignKey);

                                            if (left.HasValue)
                                                joined.AddOrUpdate(_resultSelector(foreignKey, left.Value, right), foreignKey);

                                            rightForeignKeysByKey[change.Key] = foreignKey;
                                        }
                                        else
                                        {
                                            joined.Refresh(foreignKey);
                                        }
                                    }
                                    break;
                            }
                        }

                        return joined.CaptureChanges();
                    })
                    // Don't forward initial changesets from the right side, only the left
                    .Where(_ => hasInitialized);

                lock (locker)
                {
                    var observerSubscription = leftLoader.Merge(rightLoader).SubscribeSafe(observer);

                    var rightShareConnection = rightShare.Connect();

                    hasInitialized = true;

                    return new CompositeDisposable(observerSubscription, leftCache, rightCache, rightShareConnection, leftShare.Connect(), queue);
                }
            });
}
