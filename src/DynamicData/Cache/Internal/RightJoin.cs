// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

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
                var locker = new object();

                // create local backing stores
                var leftCache = _left.Synchronize(locker).AsObservableCache(false);

                var rightShare = _right.Synchronize(locker).Publish();

                var rightCache = rightShare.AsObservableCache(false);
                var rightGrouped = rightShare.GroupWithImmutableState(_rightKeySelector).AsObservableCache(false);

                // joined is the final cache
                var joinedCache = new ChangeAwareCache<TDestination, TRightKey>();

                var rightLoader = rightCache.Connect().Select(changes =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        var leftKey = _rightKeySelector(change.Current);
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                // Update with right (and right if it is presents)
                                var rightCurrent = change.Current;
                                var leftLookup = leftCache.Lookup(leftKey);
                                joinedCache.AddOrUpdate(_resultSelector(change.Key, leftLookup, rightCurrent), change.Key);
                                break;

                            case ChangeReason.Remove:
                                // remove from result because a right value is expected
                                joinedCache.Remove(change.Key);
                                break;

                            case ChangeReason.Refresh:
                                // propagate upstream
                                joinedCache.Refresh(change.Key);
                                break;
                        }
                    }

                    return joinedCache.CaptureChanges();
                });

                var leftLoader = leftCache.Connect().Select(changes =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        var left = change.Current;
                        var right = rightGrouped.Lookup(change.Key);

                        if (right.HasValue)
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                case ChangeReason.Update:
                                    foreach (var keyvalue in right.Value.KeyValues)
                                    {
                                        joinedCache.AddOrUpdate(_resultSelector(keyvalue.Key, left, keyvalue.Value), keyvalue.Key);
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    foreach (var keyvalue in right.Value.KeyValues)
                                    {
                                        joinedCache.AddOrUpdate(_resultSelector(keyvalue.Key, Optional<TLeft>.None, keyvalue.Value), keyvalue.Key);
                                    }

                                    break;

                                case ChangeReason.Refresh:
                                    foreach (var key in right.Value.Keys)
                                    {
                                        joinedCache.Refresh(key);
                                    }

                                    break;
                            }
                        }
                    }

                    return joinedCache.CaptureChanges();
                });

                return new CompositeDisposable(leftLoader.Merge(rightLoader).SubscribeSafe(observer), leftCache, rightCache, rightShare.Connect());
            });
}
