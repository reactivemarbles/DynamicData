// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <inheritdoc cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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

        return left.LeftJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Groups right-side items by their mapped key, then left-joins each group to the left source.
    /// A result is produced for every left-side key. The right group may be empty if no right items match.
    /// Equivalent to SQL LEFT OUTER JOIN with the right side grouped.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, left value, and right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the left value and the current right group (which may be empty).</description></item>
    ///   <item><term>Update</term><description>Re-invokes the selector with the new left value and current right group.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group. If a matching left exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes the selector if a matching left exists.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If a matching left exists, re-invokes the selector (group may now be empty).</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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

        return new LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }
}
