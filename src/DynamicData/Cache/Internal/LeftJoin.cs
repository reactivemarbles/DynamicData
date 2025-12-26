// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, Optional<TRight>, TDestination> resultSelector)
    where TLeft : notnull
    where TLeftKey : notnull
    where TRight : notnull
    where TRightKey : notnull
    where TDestination : notnull
{
    private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left = left ?? throw new ArgumentNullException(nameof(left));

    private readonly Func<TLeftKey, TLeft, Optional<TRight>, TDestination> _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));

    private readonly IObservable<IChangeSet<TRight, TRightKey>> _right = right ?? throw new ArgumentNullException(nameof(right));

    private readonly Func<TRight, TLeftKey> _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));

    public IObservable<IChangeSet<TDestination, TLeftKey>> Run() => Observable.Create<IChangeSet<TDestination, TLeftKey>>(
            observer =>
            {
                var locker = InternalEx.NewLock();

                // create local backing stores
                var leftShare = _left.Synchronize(locker).Publish();
                var leftCache = leftShare.AsObservableCache(false);

                var rightShare = _right.Synchronize(locker).Publish();
                var rightCache = rightShare.AsObservableCache(false);
                var rightForeignCache = rightShare.ChangeKey(_rightKeySelector).AsObservableCache(false);

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
                                                joined.AddOrUpdate(_resultSelector(priorForeignKey, priorLeft.Value, Optional<TRight>.None), priorForeignKey);
                                        }

                                        if (left.HasValue)
                                            joined.AddOrUpdate(_resultSelector(foreignKey, left.Value, right), foreignKey);

                                        rightForeignKeysByKey[change.Key] = foreignKey;
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    if (left.HasValue)
                                        joined.AddOrUpdate(_resultSelector(foreignKey, left.Value, Optional<TRight>.None), foreignKey);

                                    rightForeignKeysByKey.Remove(change.Key);

                                    break;

                                case ChangeReason.Refresh:
                                    {
                                        if (rightForeignKeysByKey.TryGetValue(change.Key, out var priorForeignKey)
                                            && !EqualityComparer<TLeftKey>.Default.Equals(foreignKey, priorForeignKey))
                                        {
                                            var priorLeft = leftCache.Lookup(priorForeignKey);
                                            if (priorLeft.HasValue)
                                                joined.AddOrUpdate(_resultSelector(priorForeignKey, priorLeft.Value, Optional<TRight>.None), priorForeignKey);

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

                    return new CompositeDisposable(observerSubscription, leftCache, rightCache, rightShareConnection, leftShare.Connect());
                }
            });
}
