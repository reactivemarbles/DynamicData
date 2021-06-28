// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace DynamicData.Cache.Internal
{
    internal class InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>
        where TLeftKey : notnull
        where TRightKey : notnull
    {
        private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left;

        private readonly Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> _resultSelector;

        private readonly IObservable<IChangeSet<TRight, TRightKey>> _right;

        private readonly Func<TRight, TLeftKey> _rightKeySelector;

        public InnerJoinMany(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            _left = left ?? throw new ArgumentNullException(nameof(left));
            _right = right ?? throw new ArgumentNullException(nameof(right));
            _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));
            _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
        }

        public IObservable<IChangeSet<TDestination, TLeftKey>> Run()
        {
            var rightGrouped = _right.GroupWithImmutableState(_rightKeySelector);
            return _left.InnerJoin(rightGrouped, grouping => grouping.Key, (key, left, right) => _resultSelector(key.leftKey, left, right)).ChangeKey((keyTuple, _) => keyTuple.leftKey);
        }
    }
}