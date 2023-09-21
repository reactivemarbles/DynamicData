// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal class FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>
    where TLeft : notnull
    where TLeftKey : notnull
    where TRight : notnull
    where TRightKey : notnull
    where TDestination : notnull
{
    private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;

    private readonly Func<TLeftKey, Optional<TLeft>, Optional<TRight>, TDestination> _resultSelector;

    private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;

    private readonly Func<TRight, TLeftKey> _rightKeySelector;

    public FullJoin(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, Optional<TLeft>, Optional<TRight>, TDestination> resultSelector)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
        _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));
        _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
    }

    public IObservable<IChangeSet<TDestination, TLeftKey>> Run()
    {
        return Observable.Create<IChangeSet<TDestination, TLeftKey>>(
            observer =>
            {
                var locker = new object();

                // create local backing stores
                var leftCache = _left.Synchronize(locker).AsObservableCache(false);
                var rightCache = _right.Synchronize(locker).ChangeKey(_rightKeySelector).AsObservableCache(false);

                // joined is the final cache
                var joinedCache = new ChangeAwareCache<TDestination, TLeftKey>();

                var leftLoader = leftCache.Connect().Select(changes =>
                {
                    foreach (var change in changes.ToConcreteType())
                    {
                        var left = change.Current;
                        var right = rightCache.Lookup(change.Key);

                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                joinedCache.AddOrUpdate(_resultSelector(change.Key, left, right), change.Key);
                                break;

                            case ChangeReason.Remove:

                                if (!right.HasValue)
                                {
                                    // remove from result because there is no left and no rights
                                    joinedCache.Remove(change.Key);
                                }
                                else
                                {
                                    // update with no left value
                                    joinedCache.AddOrUpdate(_resultSelector(change.Key, Optional<TLeft>.None, right), change.Key);
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
                                        joinedCache.AddOrUpdate(_resultSelector(change.Key, left, Optional<TRight>.None), change.Key);
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

                return new CompositeDisposable(leftLoader.Merge(rightLoader).SubscribeSafe(observer), leftCache, rightCache);
            });
    }
}
