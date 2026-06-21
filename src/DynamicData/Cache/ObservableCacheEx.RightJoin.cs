// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <inheritdoc cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left and right values into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TRightKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<ReactiveUI.Primitives.Optional<TLeft>, TRight, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(left);
        ArgumentExceptionHelper.ThrowIfNull(right);
        ArgumentExceptionHelper.ThrowIfNull(rightKeySelector);
        ArgumentExceptionHelper.ThrowIfNull(resultSelector);

        return left.RightJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Joins two changeset streams, producing a result for every right-side key. The left side is
    /// <see cref="Optional{T}"/> because a matching left item may or may not exist. All right items
    /// appear in the output regardless. Equivalent to SQL RIGHT OUTER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the right key, optional left, and right value into a destination object. Example: <c>(rightKey, left, right) =&gt; new Result(rightKey, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TRightKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the matching left (or <see cref="ReactiveUI.Primitives.Optional.None{T}"/>) and the right value.</description></item>
    ///   <item><term>Update</term><description>Re-invokes the selector with current left (if any) and the new right value.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If matching right items exist, re-invokes the selector (left transitions from None to Some) and emits Updates.</description></item>
    ///   <item><term>Update</term><description>If matching right items exist, re-invokes the selector with the new left value.</description></item>
    ///   <item><term>Remove</term><description>If matching right items exist, re-invokes the selector (left transitions from Some to None) and emits Updates.</description></item>
    ///   <item><term>Refresh</term><description>If joined results exist, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, TRightKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TRightKey, ReactiveUI.Primitives.Optional<TLeft>, TRight, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(left);
        ArgumentExceptionHelper.ThrowIfNull(right);
        ArgumentExceptionHelper.ThrowIfNull(rightKeySelector);
        ArgumentExceptionHelper.ThrowIfNull(resultSelector);

        return new RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }
}
