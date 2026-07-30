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
    /// Provides an overload of <c>LeftJoin</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TLeft">The type of the TLeft value.</typeparam>
    /// <typeparam name="TLeftKey">The type of the TLeftKey value.</typeparam>
    /// <typeparam name="TRight">The type of the TRight value.</typeparam>
    /// <typeparam name="TRightKey">The type of the TRightKey value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="left">The left <c>IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;</c> to join.</param>
    /// <param name="right">The right <c>IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;</c> to join.</param>
    /// <param name="rightKeySelector">A <c>Func&lt;T, TResult&gt;</c> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <c>Func&lt;T, TResult&gt;</c> that combines the left value and the optional right into a destination object. The key is not provided in this overload.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload that omits the key from the result selector. Delegates to <c>LeftJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, TLeft, Optional&lt;TRight&gt;, TDestination&gt;)</c>.</remarks>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, ReactiveUI.Primitives.Optional<TRight>, TDestination> resultSelector)
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

        return left.LeftJoin(right, rightKeySelector, (_, leftValue, rightValue) => resultSelector(leftValue, rightValue));
    }

    /// <summary>
    /// Joins two changeset streams, producing a result for every left-side key. The right side is
    /// <c>Optional&lt;T&gt;</c> because a matching right item may or may not exist. All left items
    /// appear in the output regardless. Equivalent to SQL LEFT OUTER JOIN.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <c>IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;</c> to join.</param>
    /// <param name="right">The right <c>IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;</c> to join.</param>
    /// <param name="rightKeySelector">A <c>Func&lt;T, TResult&gt;</c> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <c>Func&lt;T, TResult&gt;</c> that combines the key, left value, and optional right into a destination object. Example: <c>(key, left, right) =&gt; new Result(key, left, right)</c>.</param>
    /// <returns>An observable changeset keyed by <typeparamref name="TLeftKey"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Left-side change handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Always emits. Invokes <paramref name="resultSelector"/> with the left value and matching right (or <c>ReactiveUI.Primitives.Optional.None&lt;T&gt;</c>).</description></item>
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
    /// <seealso><c>InnerJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;ValueTuple&lt;TLeftKey, TRightKey&gt;, TLeft, TRight, TDestination&gt;)</c></seealso>
    /// <seealso><c>RightJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TRightKey, Optional&lt;TLeft&gt;, TRight, TDestination&gt;)</c></seealso>
    /// <seealso><c>FullJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, Optional&lt;TLeft&gt;, Optional&lt;TRight&gt;, TDestination&gt;)</c></seealso>
    /// <seealso><c>LeftJoinMany&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, TLeft, IGrouping&lt;TRight, TRightKey, TLeftKey&gt;, TDestination&gt;)</c></seealso>
    public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeftKey, TLeft, ReactiveUI.Primitives.Optional<TRight>, TDestination> resultSelector)
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

        return new LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }
}
