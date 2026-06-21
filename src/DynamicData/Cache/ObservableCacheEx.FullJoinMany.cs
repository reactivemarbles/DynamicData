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
    /// <inheritdoc cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<ReactiveUI.Primitives.Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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

        return left.FullJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Groups right-side items by their mapped key, then full-joins each group to the left source.
    /// A result is produced for every key that appears on either side (or both). The left value is
    /// <see cref="Optional{T}"/> because only the right side may have entries for a given key.
    /// Equivalent to SQL FULL OUTER JOIN with the right side grouped.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, optional left value, and the right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Emits with the left value and the current right group for that key (may be empty).</description></item>
    ///   <item><term>Update</term><description>Re-invokes <paramref name="resultSelector"/> with the new left value and current right group.</description></item>
    ///   <item><term>Remove</term><description>If the right group is non-empty, re-invokes with left as <see cref="ReactiveUI.Primitives.Optional.None{T}"/>. If both sides are empty, removes the result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group, then re-invokes selector with the current left (if any) and the updated group.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes selector.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If the group becomes empty and no left exists, removes the result. Otherwise re-invokes selector.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, ReactiveUI.Primitives.Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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

        return new FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }
}
