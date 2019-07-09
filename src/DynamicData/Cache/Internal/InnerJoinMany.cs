using System;

namespace DynamicData.Cache.Internal
{
    internal class InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;
        private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;
        private readonly Func<TRight, TLeftKey> _rightKeySelector;
        private readonly Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> _resultSelector;

        public InnerJoinMany(IObservable<IChangeSet<TLeft, TLeftKey>> left,
            IObservable<IChangeSet<TRight, TRightKey>> right,
            Func<TRight, TLeftKey> rightKeySelector,
            Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right ?? throw new ArgumentNullException(nameof(right));
            _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));
            _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
        }
        
        public IObservable<IChangeSet<TDestination, TLeftKey>> Run()
        {
            var rightGrouped = _right.GroupWithImmutableState(_rightKeySelector);
            return _left.InnerJoin(rightGrouped, grouping => grouping.Key, _resultSelector);
        }
    }
}