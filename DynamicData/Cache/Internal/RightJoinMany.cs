using System;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;
        private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;
        private readonly Func<TRight, TLeftKey> _rightKeySelector;
        private readonly Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> _resultSelector;

        public RightJoinMany([NotNull] IObservable<IChangeSet<TLeft, TLeftKey>> left,
            [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
            [NotNull] Func<TRight, TLeftKey> rightKeySelector,
            [NotNull] Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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
            var rightGrouped = _right.GroupWithImmutableState(_rightKeySelector);
            return _left.RightJoin(rightGrouped, grouping => grouping.Key, (a, b, c) => _resultSelector(a, b, c));
        }
    }
}