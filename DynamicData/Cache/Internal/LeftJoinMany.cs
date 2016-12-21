using System;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;
        private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;
        private readonly Func<TRight, TLeftKey> _rightKeySelector;
        private readonly Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> _resultSelector;

        public LeftJoinMany(IObservable<IChangeSet<TLeft, TLeftKey>> left,
            IObservable<IChangeSet<TRight, TRightKey>> right,
            Func<TRight, TLeftKey> rightKeySelector,
            Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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
            var emptyCache = Cache<TRight, TRightKey>.Empty;
            var rightGrouped = _right.GroupWithImmutableState(_rightKeySelector);
            return _left.LeftJoin(rightGrouped, grouping => grouping.Key,
                (leftKey, left, grouping) => _resultSelector(leftKey, left, grouping.ValueOr(() =>
                {
                    return new ImmutableGroup<TRight, TRightKey, TLeftKey>(leftKey, emptyCache);
                })));
        }

    }
}