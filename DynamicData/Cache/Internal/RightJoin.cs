using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;
        private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;
        private readonly Func<TRight, TLeftKey> _rightKeySelector;
        private readonly Func<TLeftKey, Optional<TLeft>, TRight, TDestination> _resultSelector;

        public RightJoin(IObservable<IChangeSet<TLeft, TLeftKey>> left,
            IObservable<IChangeSet<TRight, TRightKey>> right,
            Func<TRight, TLeftKey> rightKeySelector,
            Func<TLeftKey, Optional<TLeft>, TRight, TDestination> resultSelector)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right ?? throw new ArgumentNullException(nameof(right));
            _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));
            _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
        }

        public IObservable<IChangeSet<TDestination, TLeftKey>> Run()
        {
            return Observable.Create<IChangeSet<TDestination, TLeftKey>>(observer =>
            {
                var locker = new object();

                //create local backing stores
                var leftCache = _left.Synchronize(locker).AsObservableCache(false);
                var rightCache = _right.Synchronize(locker).ChangeKey(_rightKeySelector).AsObservableCache(false);

                //joined is the final cache
                var joinedCache = new LockFreeObservableCache<TDestination, TLeftKey>();

                var rightLoader = rightCache.Connect()
                    .Subscribe(changes =>
                    {
                        joinedCache.Edit(innerCache =>
                        {
                            changes.ForEach(change =>
                            {
                                switch (change.Reason)
                                {
                                    case ChangeReason.Add:
                                    case ChangeReason.Update:
                                        //Update with right (and right if it is presents)
                                        var right = change.Current;
                                        var left = leftCache.Lookup(change.Key);
                                        innerCache.AddOrUpdate(_resultSelector(change.Key, left, right), change.Key);
                                        break;
                                    case ChangeReason.Remove:
                                        //remove from result because a right value is expected
                                        innerCache.Remove(change.Key);
                                        break;
                                    case ChangeReason.Refresh:
                                        //propagate upstream
                                        innerCache.Refresh(change.Key);
                                        break;
                                }
                            });
                        });
                    });

                var leftLoader = leftCache.Connect()
                    .Subscribe(changes =>
                    {
                        joinedCache.Edit(innerCache =>
                        {
                            changes.ForEach(change =>
                            {
                                TLeft left = change.Current;
                                Optional<TRight> right = rightCache.Lookup(change.Key);

                                switch (change.Reason)
                                {
                                    case ChangeReason.Add:
                                    case ChangeReason.Update:
                                        {
                                            if (right.HasValue)
                                            {
                                                //Update with left and right value
                                                innerCache.AddOrUpdate(_resultSelector(change.Key, left, right.Value), change.Key);
                                            }
                                            else
                                            {
                                                //There is no right so remove if  already in the cache
                                                innerCache.Remove(change.Key);
                                            }
                                        }
                                        break;
                                    case ChangeReason.Remove:
                                        {
                                            if (right.HasValue)
                                            {
                                                //Update with no left value
                                                innerCache.AddOrUpdate(_resultSelector(change.Key, Optional<TLeft>.None, right.Value), change.Key);
                                            }
                                            else
                                            {
                                                //remove if it is already in the cache
                                                innerCache.Remove(change.Key);
                                            }
                                        }
                                        break;
                                    case ChangeReason.Refresh:
                                        //propagate upstream
                                        innerCache.Refresh(change.Key);
                                        break;
                                }
                            });
                        });
                    });


                return new CompositeDisposable(
                    joinedCache.Connect().NotEmpty().SubscribeSafe(observer),
                    leftCache,
                    rightCache,
                    rightLoader,
                    joinedCache,
                    leftLoader);
            });
        }
    }
}