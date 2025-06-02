// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<(TLeftKey leftKey, TRightKey rightKey), TLeft, TRight, TDestination> resultSelector)
    where TLeft : notnull
    where TLeftKey : notnull
    where TRight : notnull
    where TRightKey : notnull
    where TDestination : notnull
{
    private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left = left ?? throw new ArgumentNullException(nameof(left));

    private readonly Func<(TLeftKey leftKey, TRightKey rightKey), TLeft, TRight, TDestination> _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));

    private readonly IObservable<IChangeSet<TRight, TRightKey>> _right = right ?? throw new ArgumentNullException(nameof(right));

    private readonly Func<TRight, TLeftKey> _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));

    public IObservable<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>> Run() => Observable.Create<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>>(
            observer =>
            {
                var locker = InternalEx.NewLock();

                // create local backing stores
                var leftCache = _left.Synchronize(locker).AsObservableCache(false);

                var rightShare = _right.Synchronize(locker).Publish();
                var rightCache = rightShare.AsObservableCache(false);
                var rightGrouped = rightShare.GroupWithImmutableState(_rightKeySelector).AsObservableCache(false);

                var rightForeignKeysByKey = new Dictionary<TRightKey, TLeftKey>();

                // joined is the final cache
                var joinedCache = new ChangeAwareCache<TDestination, (TLeftKey, TRightKey)>();

                var hasInitialized = false;

                var leftLoader = leftCache.Connect()
                    .Select(changes =>
                    {
                        foreach (var change in changes.ToConcreteType())
                        {
                            var leftCurrent = change.Current;
                            var rightLookup = rightGrouped.Lookup(change.Key);

                            if (rightLookup.HasValue)
                            {
                                switch (change.Reason)
                                {
                                    case ChangeReason.Add:
                                    case ChangeReason.Update:
                                        foreach (var keyvalue in rightLookup.Value.KeyValues)
                                        {
                                            joinedCache.AddOrUpdate(_resultSelector((change.Key, keyvalue.Key), leftCurrent, keyvalue.Value), (change.Key, keyvalue.Key));
                                        }

                                        break;

                                    case ChangeReason.Remove:
                                        foreach (var keyvalue in rightLookup.Value.KeyValues)
                                        {
                                            joinedCache.Remove((change.Key, keyvalue.Key));
                                        }

                                        break;

                                    case ChangeReason.Refresh:
                                        foreach (var key in rightLookup.Value.Keys)
                                        {
                                            joinedCache.Refresh((change.Key, key));
                                        }

                                        break;
                                }
                            }
                        }

                        return joinedCache.CaptureChanges();
                    })
                    // Don't forward initial changesets from the left side, only the right
                    .Where(_ => hasInitialized);

                var rightLoader = rightCache.Connect().Select(changes =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        var leftKey = _rightKeySelector(change.Current);
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                {
                                    // If this is an update and the foreign key has changed, we need to remove the old pairing before attempting to add a new one.
                                    if (rightForeignKeysByKey.TryGetValue(change.Key, out var oldLeftKey)
                                        && !EqualityComparer<TLeftKey>.Default.Equals(leftKey, oldLeftKey)
                                        && leftCache.Lookup(oldLeftKey).HasValue)
                                    {
                                        joinedCache.Remove((oldLeftKey, change.Key));
                                    }

                                    // If the new item has a pairing, either add or update it
                                    rightForeignKeysByKey[change.Key] = leftKey;
                                    var right = change.Current;
                                    var left = leftCache.Lookup(leftKey);
                                    if (left.HasValue)
                                    {
                                        joinedCache.AddOrUpdate(_resultSelector((leftKey, change.Key), left.Value, right), (leftKey, change.Key));
                                    }
                                }
                                break;

                            case ChangeReason.Remove:
                                // remove from result because a right value is expected
                                rightForeignKeysByKey.Remove(change.Key);
                                joinedCache.Remove((leftKey, change.Key));
                                break;

                            case ChangeReason.Refresh:
                                {
                                    // Check to see if the foreign key has changed, and re-pair the item, if so
                                    var oldLeftKey = rightForeignKeysByKey[change.Key];
                                    rightForeignKeysByKey[change.Key] = leftKey;
                                    if (!EqualityComparer<TLeftKey>.Default.Equals(leftKey, oldLeftKey))
                                    {
                                        if (leftCache.Lookup(oldLeftKey).HasValue)
                                        {
                                            joinedCache.Remove((oldLeftKey, change.Key));
                                        }

                                        var left = leftCache.Lookup(leftKey);
                                        if (left.HasValue)
                                        {
                                            joinedCache.AddOrUpdate(_resultSelector((leftKey, change.Key), left.Value, change.Current), (leftKey, change.Key));
                                        }
                                    }
                                    else
                                    {
                                        joinedCache.Refresh((leftKey, change.Key));
                                    }
                                }
                                break;
                        }
                    }

                    return joinedCache.CaptureChanges();
                });

                lock (locker)
                {
                    var observerSubscription = leftLoader.Merge(rightLoader).SubscribeSafe(observer);

                    hasInitialized = true;

                    return new CompositeDisposable(observerSubscription, leftCache, rightCache, rightShare.Connect());
                }
            });
}
