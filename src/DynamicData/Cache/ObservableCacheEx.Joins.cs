// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// ObservableCache extensions for join operators (FullJoin, InnerJoin, LeftJoin, RightJoin).
/// </summary>
public static partial class ObservableCacheEx
{
    /// <inheritdoc cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left and right values into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, Optional<TRight>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return left.FullJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Joins two changeset streams, producing a result for every key that appears on either side (or both).
    /// Both sides are <see cref="Optional{T}"/> because a given key may only exist on one side at any point.
    /// Equivalent to SQL FULL OUTER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, optional left, and optional right into a destination object. Example: <c>(key, left, right) =&gt; new Result(key, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Emits with the left value and the matching right (or <see cref="Optional.None{T}"/> if no right exists).</description></item>
    ///   <item><term>Update</term><description>Re-invokes <paramref name="resultSelector"/> with the new left value and current right (if any).</description></item>
    ///   <item><term>Remove</term><description>If a right match still exists, re-invokes the selector with left as <see cref="Optional.None{T}"/>. If neither side remains, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Emits with the matching left (or <see cref="Optional.None{T}"/>) and the right value.</description></item>
    ///   <item><term>Update</term><description>Re-invokes selector with current left (if any) and the new right value.</description></item>
    ///   <item><term>Remove</term><description>If a left match still exists, re-invokes the selector with right as <see cref="Optional.None{T}"/>. If neither side remains, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, Optional<TLeft>, Optional<TRight>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

    /// <inheritdoc cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
    ///   <item><term>Remove</term><description>If the right group is non-empty, re-invokes with left as <see cref="Optional.None{T}"/>. If both sides are empty, removes the result.</description></item>
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
    public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

    /// <inheritdoc cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the left and right values into a destination object. The composite key is not provided in this overload.</param>
    /// <remarks>Overload that omits the composite key from the result selector. Delegates to <see cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, TRight, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return left.InnerJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Joins two changeset streams, producing a result only for keys that exist on both sides simultaneously.
    /// When either side loses its value for a key, the joined result is removed. Equivalent to SQL INNER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the composite key, left value, and right value into a destination object. Example: <c>((leftKey, rightKey), left, right) =&gt; new Result(leftKey, rightKey, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by a composite <c>(TLeftKey, TRightKey)</c> tuple.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a matching right value exists, invokes <paramref name="resultSelector"/> and emits an Add. If no right match, no emission.</description></item>
    ///   <item><term>Update</term><description>If a matching right exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Remove</term><description>Removes all joined results involving the removed left key.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a matching left value exists, invokes the selector and emits an Add.</description></item>
    ///   <item><term>Update</term><description>If a matching left exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result for this right key (if it was downstream).</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>The output is keyed by a <c>(TLeftKey, TRightKey)</c> composite tuple, since a single left item may match multiple right items.</para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<(TLeftKey leftKey, TRightKey rightKey), TLeft, TRight, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

    /// <inheritdoc cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return left.InnerJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Groups right-side items by their mapped key, then inner-joins each group to the left source.
    /// A result is produced only when a left item and at least one right item share the same key.
    /// Equivalent to SQL INNER JOIN with the right side grouped.
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
    ///   <item><term>Add</term><description>If a non-empty right group exists for this key, invokes <paramref name="resultSelector"/> and emits an Add. Otherwise no emission.</description></item>
    ///   <item><term>Update</term><description>If a right group exists, re-invokes the selector and emits an Update.</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result (if it was downstream).</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group. If a matching left exists and the group was previously empty, emits an Add. If already joined, emits an Update.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes the selector if a matching left exists.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If the group becomes empty, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

    /// <inheritdoc cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the left value and the optional right into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="LeftJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, Optional{TRight}, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, Optional<TRight>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return left.LeftJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Joins two changeset streams, producing a result for every left-side key. The right side is
    /// <see cref="Optional{T}"/> because a matching right item may or may not exist. All left items
    /// appear in the output regardless. Equivalent to SQL LEFT OUTER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, left value, and optional right into a destination object. Example: <c>(key, left, right) =&gt; new Result(key, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the left value and matching right (or <see cref="Optional.None{T}"/>).</description></item>
    ///   <item><term>Update</term><description>Re-invokes the selector with the new left value and current right (if any).</description></item>
    ///   <item><term>Remove</term><description>Removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh on the joined result.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a matching left exists, re-invokes the selector (right transitions from None to Some) and emits an Update.</description></item>
    ///   <item><term>Update</term><description>If a matching left exists, re-invokes the selector with the new right value.</description></item>
    ///   <item><term>Remove</term><description>If a matching left exists, re-invokes the selector (right transitions from Some to None) and emits an Update.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="InnerJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{ValueTuple{TLeftKey, TRightKey}, TLeft, TRight, TDestination})"/>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="FullJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, Optional{TRight}, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, Optional<TRight>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

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
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

    /// <inheritdoc cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left and right values into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TRightKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, TRight, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

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
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the matching left (or <see cref="Optional.None{T}"/>) and the right value.</description></item>
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
    public static IObservable<IChangeSet<TDestination, TRightKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TRightKey, Optional<TLeft>, TRight, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }

    /// <inheritdoc cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the optional left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <see cref="RightJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return left.RightJoinMany(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Groups right-side items by their mapped key, then right-joins each group to the left source.
    /// A result is produced for every key that has at least one right item. The left value is
    /// <see cref="Optional{T}"/> because a matching left item may or may not exist.
    /// Equivalent to SQL RIGHT OUTER JOIN with the right side grouped.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <see cref="IObservable{IChangeSet{TLeft, TLeftKey}}"/> to join.</param>
    /// <param name="right">The right <see cref="IObservable{IChangeSet{TRight, TRightKey}}"/> to join.</param>
    /// <param name="rightKeySelector">A <see cref="Func{T, TResult}"/> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> that combines the key, optional left value, and right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Right-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Updates the right group. If the group was previously empty, emits an Add with the current left (if any). Otherwise emits an Update.</description></item>
    ///   <item><term>Update</term><description>Updates the right group and re-invokes <paramref name="resultSelector"/>.</description></item>
    ///   <item><term>Remove</term><description>Updates the right group. If the group becomes empty, removes the joined result.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>If a non-empty right group exists, re-invokes the selector (left transitions from None to Some) and emits an Update.</description></item>
    ///   <item><term>Update</term><description>If a non-empty right group exists, re-invokes the selector with the new left value.</description></item>
    ///   <item><term>Remove</term><description>If a non-empty right group exists, re-invokes the selector (left transitions from Some to None) and emits an Update.</description></item>
    ///   <item><term>Refresh</term><description>If a joined result exists, forwarded as Refresh.</description></item>
    /// </list>
    /// </para>
    /// <para>Both sources are serialized through a shared lock held during downstream delivery. Avoid blocking operations in subscribers.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <seealso cref="RightJoin{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TRightKey, Optional{TLeft}, TRight, TDestination})"/>
    /// <seealso cref="InnerJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="LeftJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, TLeft, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    /// <seealso cref="FullJoinMany{TLeft, TLeftKey, TRight, TRightKey, TDestination}(IObservable{IChangeSet{TLeft, TLeftKey}}, IObservable{IChangeSet{TRight, TRightKey}}, Func{TRight, TLeftKey}, Func{TLeftKey, Optional{TLeft}, IGrouping{TRight, TRightKey, TLeftKey}, TDestination})"/>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        where TLeft : notnull
        where TLeftKey : notnull
        where TRight : notnull
        where TRightKey : notnull
        where TDestination : notnull
    {
        left.ThrowArgumentNullExceptionIfNull(nameof(left));
        right.ThrowArgumentNullExceptionIfNull(nameof(right));
        rightKeySelector.ThrowArgumentNullExceptionIfNull(nameof(rightKeySelector));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return new RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }
}
