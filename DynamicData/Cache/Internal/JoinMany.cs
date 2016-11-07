using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{

    internal class JoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;
        private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;
        private readonly Func<TRight, TLeftKey> _rightKeySelector;

        private readonly Func<TLeftKey, Optional<TLeft>, IObservableCache<TRight, TRightKey>, TDestination> _resultSelector;

        public JoinMany(IObservable<IChangeSet<TLeft, TLeftKey>> left,
            IObservable<IChangeSet<TRight, TRightKey>> right,
            Func<TRight, TLeftKey> rightKeySelector,
            Func<TLeftKey, Optional<TLeft>, IObservableCache<TRight, TRightKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            _left = left;
            _right = right;
            _rightKeySelector = rightKeySelector;
            _resultSelector = resultSelector;
        }

        public IObservable<IChangeSet<TDestination, TLeftKey>> Run()
        {
            return Observable.Create<IChangeSet<TDestination, TLeftKey>>(observer =>
            {
                var locker = new object();

                //create local backing stores
                var leftCache = _left.Synchronize(locker);
                var rightCache = _right.Synchronize(locker).Group(_rightKeySelector);//.AsObservableCache(false);

                //joined is the final cache
            //    var joinedCache = new LockFreeObservableCache<TDestination, TLeftKey>();

            //    //TODO: 1) group and join 2) 
            //    var xddd =  leftCache.FullJoin(rightCache, _rightKeySelector, (x, b) =>
            //    {
            //        return _resultSelector(x,b,c);
            //    });
            ////  var rightGrouped = rightCache.

            //    return new CompositeDisposable(leftCache, rightCache, 
            //        joinedCache.Connect().SubscribeSafe(observer));


                return new CompositeDisposable();
            });
        }
    }
}