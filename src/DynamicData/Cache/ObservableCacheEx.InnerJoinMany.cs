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
    /// <summary>
    /// Provides an overload of <c>InnerJoinMany</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TLeft">The type of the TLeft value.</typeparam>
    /// <typeparam name="TLeftKey">The type of the TLeftKey value.</typeparam>
    /// <typeparam name="TRight">The type of the TRight value.</typeparam>
    /// <typeparam name="TRightKey">The type of the TRightKey value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="left">The left <c>IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;</c> to join.</param>
    /// <param name="right">The right <c>IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;</c> to join.</param>
    /// <param name="rightKeySelector">A <c>Func&lt;T, TResult&gt;</c> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <c>Func&lt;T, TResult&gt;</c> that combines the left value and the right group into a destination object. The key is not provided in this overload.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <c>InnerJoinMany&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, TLeft, IGrouping&lt;TRight, TRightKey, TLeftKey&gt;, TDestination&gt;)</c>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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
    /// <param name="left">The left <c>IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;</c> to join.</param>
    /// <param name="right">The right <c>IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;</c> to join.</param>
    /// <param name="rightKeySelector">A <c>Func&lt;T, TResult&gt;</c> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <c>Func&lt;T, TResult&gt;</c> that combines the key, left value, and right group into a destination object. Example: <c>(key, left, group) =&gt; new Result(key, left, group)</c>.</param>
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
    /// <seealso><c>InnerJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;ValueTuple&lt;TLeftKey, TRightKey&gt;, TLeft, TRight, TDestination&gt;)</c></seealso>
    /// <seealso><c>LeftJoinMany&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, TLeft, IGrouping&lt;TRight, TRightKey, TLeftKey&gt;, TDestination&gt;)</c></seealso>
    /// <seealso><c>RightJoinMany&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, Optional&lt;TLeft&gt;, IGrouping&lt;TRight, TRightKey, TLeftKey&gt;, TDestination&gt;)</c></seealso>
    /// <seealso><c>FullJoinMany&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, Optional&lt;TLeft&gt;, IGrouping&lt;TRight, TRightKey, TLeftKey&gt;, TDestination&gt;)</c></seealso>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
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

        return new InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }
}
