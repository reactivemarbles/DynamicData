using System;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;
        private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;
        private readonly Func<TRight, TLeftKey> _rightKeySelector;
        private readonly Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> _resultSelector;

        public FullJoinMany([NotNull] IObservable<IChangeSet<TLeft, TLeftKey>> left,
            [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
            [NotNull] Func<TRight, TLeftKey> rightKeySelector,
            [NotNull] Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right ?? throw new ArgumentNullException(nameof(right));
            _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));
            _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
        }

        public IObservable<IChangeSet<TDestination, TLeftKey>> Run()
        {
            var emptyCache = Cache<TRight, TRightKey>.Empty;

            var rightGrouped = _right.GroupWithImmutableState(_rightKeySelector);
            return _left.FullJoin(rightGrouped, grouping => grouping.Key,
                (leftKey, left, grouping) => _resultSelector(leftKey, left, grouping.ValueOr(() =>
                {
                    return new ImmutableGroup<TRight, TRightKey, TLeftKey>(leftKey, emptyCache);
                })));
        }
    }
}