// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TRightKey, Optional<TLeft>, TRight, TDestination> resultSelector)
    where TLeft : notnull
    where TLeftKey : notnull
    where TRight : notnull
    where TRightKey : notnull
    where TDestination : notnull
{
    private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left = left ?? throw new ArgumentNullException(nameof(left));

    private readonly Func<TRightKey, Optional<TLeft>, TRight, TDestination> _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));

    private readonly IObservable<IChangeSet<TRight, TRightKey>> _right = right ?? throw new ArgumentNullException(nameof(right));

    private readonly Func<TRight, TLeftKey> _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));

    public IObservable<IChangeSet<TDestination, TRightKey>> Run() => Observable.Create<IChangeSet<TDestination, TRightKey>>(
            observer =>
            {
                var locker = InternalEx.NewLock();

                // create local backing stores
                var leftCache = _left.Synchronize(locker).AsObservableCache(false);

                var rightShare = _right.Synchronize(locker).Publish();

                var rightCache = rightShare.AsObservableCache(false);
                var rightForeignCache = rightShare
                    .Transform(static (item, key) => (item, key))
                    .ChangeKey(pair => _rightKeySelector.Invoke(pair.item))
                    .AsObservableCache(false);

                var rightForeignKeysByKey = new Dictionary<TRightKey, TLeftKey>();

                // joined is the final cache
                var joinedCache = new ChangeAwareCache<TDestination, TRightKey>();

                var hasInitialized = false;

                var rightLoader = rightCache.Connect().Select(changes =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        var foreignKey = _rightKeySelector(change.Current);
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                // Update with right (and right if it is presents)
                                var rightCurrent = change.Current;
                                var leftLookup = leftCache.Lookup(foreignKey);
                                joinedCache.AddOrUpdate(_resultSelector(change.Key, leftLookup, rightCurrent), change.Key);

                                rightForeignKeysByKey[change.Key] = foreignKey;
                                break;

                            case ChangeReason.Remove:
                                // remove from result because a right value is expected
                                joinedCache.Remove(change.Key);
                                rightForeignKeysByKey.Remove(change.Key);
                                break;

                            case ChangeReason.Refresh:
                                if (rightForeignKeysByKey.TryGetValue(change.Key, out var priorForeignKey)
                                    && !EqualityComparer<TLeftKey>.Default.Equals(foreignKey, priorForeignKey))
                                {
                                    joinedCache.AddOrUpdate(_resultSelector(change.Key, leftCache.Lookup(foreignKey), change.Current), change.Key);

                                    rightForeignKeysByKey[change.Key] = foreignKey;
                                }
                                else
                                {
                                    // propagate downstream
                                    joinedCache.Refresh(change.Key);
                                }
                                break;
                        }
                    }

                    return joinedCache.CaptureChanges();
                });

                var leftLoader = leftCache.Connect()
                    .Select(changes =>
                    {
                        foreach (var change in changes.ToConcreteType())
                        {
                            var left = change.Current;
                            var right = rightForeignCache.Lookup(change.Key);

                            if (right.HasValue)
                            {
                                switch (change.Reason)
                                {
                                    case ChangeReason.Add:
                                    case ChangeReason.Update:
                                        if (right.HasValue)
                                        {
                                            joinedCache.AddOrUpdate(_resultSelector(right.Value.key!, left, right.Value.item), right.Value.key);
                                        }

                                        break;

                                    case ChangeReason.Remove:
                                        if (right.HasValue)
                                        {
                                            joinedCache.AddOrUpdate(_resultSelector(right.Value.key, Optional<TLeft>.None, right.Value.item), right.Value.key);
                                        }

                                        break;

                                    case ChangeReason.Refresh:
                                        if (right.HasValue)
                                        {
                                            joinedCache.Refresh(right.Value.key);
                                        }

                                        break;
                                }
                            }
                        }

                        return joinedCache.CaptureChanges();
                    })
                    // Don't forward initial changesets from the left side, only the right
                    .Where(_ => hasInitialized);

                lock (locker)
                {
                    var observerSubscription = leftLoader.Merge(rightLoader).SubscribeSafe(observer);

                    hasInitialized = true;

                    return new CompositeDisposable(observerSubscription, leftCache, rightCache, rightShare.Connect());
                }
            });
}
