// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

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
                var locker = new object();

                // create local backing stores
                var leftCache = _left.Synchronize(locker).AsObservableCache(false);
                var rightCache = _right.Synchronize(locker).ChangeKey(_rightKeySelector).AsObservableCache(false);

                // joined is the final cache
                var joined = new ChangeAwareCache<TDestination, TLeftKey>();

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
                                    var rightLookup = rightCache.Lookup(change.Key);
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

                var rightLoader = rightCache.Connect().Select(
                    changes =>
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
                                        if (left.HasValue)
                                        {
                                            // Update with left and right value
                                            joined.AddOrUpdate(_resultSelector(change.Key, left.Value, right), change.Key);
                                        }
                                        else
                                        {
                                            // remove if it is already in the cache
                                            joined.Remove(change.Key);
                                        }
                                    }

                                    break;

                                case ChangeReason.Remove:
                                    {
                                        if (left.HasValue)
                                        {
                                            // Update with no right value
                                            joined.AddOrUpdate(_resultSelector(change.Key, left.Value, Optional<TRight>.None), change.Key);
                                        }
                                        else
                                        {
                                            // remove if it is already in the cache
                                            joined.Remove(change.Key);
                                        }
                                    }

                                    break;

                                case ChangeReason.Refresh:
                                    // propagate upstream
                                    joined.Refresh(change.Key);
                                    break;
                            }
                        }

                        return joined.CaptureChanges();
                    });

                lock (locker)
                {
                    return new CompositeDisposable(leftLoader.Merge(rightLoader).SubscribeSafe(observer), leftCache, rightCache);
                }
            });
}
