// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the RightJoinMany class.
/// </summary>
/// <typeparam name="TLeft">The type of the TLeft value.</typeparam>
/// <typeparam name="TLeftKey">The type of the TLeftKey value.</typeparam>
/// <typeparam name="TRight">The type of the TRight value.</typeparam>
/// <typeparam name="TRightKey">The type of the TRightKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <param name="left">The left value.</param>
/// <param name="right">The right value.</param>
/// <param name="rightKeySelector">The rightKeySelector value.</param>
/// <param name="resultSelector">The resultSelector value.</param>
internal sealed class RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, ReactiveUI.Primitives.Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
    where TLeft : notnull
    where TLeftKey : notnull
    where TRight : notnull
    where TRightKey : notnull
    where TDestination : notnull
{
    /// <summary>
    /// The _left field.
    /// </summary>
    private readonly IObservable<IChangeSet<TLeft, TLeftKey>> _left = left ?? throw new ArgumentNullException(nameof(left));

    /// <summary>
    /// The _resultSelector field.
    /// </summary>
    private readonly Func<TLeftKey, ReactiveUI.Primitives.Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> _resultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));

    /// <summary>
    /// The _right field.
    /// </summary>
    private readonly IObservable<IChangeSet<TRight, TRightKey>> _right = right ?? throw new ArgumentNullException(nameof(right));

    /// <summary>
    /// The _rightKeySelector field.
    /// </summary>
    private readonly Func<TRight, TLeftKey> _rightKeySelector = rightKeySelector ?? throw new ArgumentNullException(nameof(rightKeySelector));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TLeftKey>> Run()
    {
        var rightGrouped = _right.GroupWithImmutableState(_rightKeySelector);
        return _left.RightJoin(rightGrouped, grouping => grouping.Key, (a, b, c) => _resultSelector(a, b, c));
    }
}
