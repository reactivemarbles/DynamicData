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
    /// Joins two changeset streams and projects matching left and right values without exposing the composite key to the selector.
    /// </summary>
    /// <typeparam name="TLeft">The item type of the left source.</typeparam>
    /// <typeparam name="TLeftKey">The key type of the left source.</typeparam>
    /// <typeparam name="TRight">The item type of the right source.</typeparam>
    /// <typeparam name="TRightKey">The key type of the right source.</typeparam>
    /// <typeparam name="TDestination">The type produced by <paramref name="resultSelector"/>.</typeparam>
    /// <param name="left">The left <c>IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;</c> to join.</param>
    /// <param name="right">The right <c>IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;</c> to join.</param>
    /// <param name="rightKeySelector">A <c>Func&lt;T, TResult&gt;</c> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <c>Func&lt;T, TResult&gt;</c> that combines the left and right values into a destination object. The composite key is not provided in this overload.</param>
    /// <returns>An observable changeset keyed by a composite <c>(TLeftKey, TRightKey)</c> tuple.</returns>
    /// <remarks>Overload that omits the composite key from the result selector. Delegates to <c>InnerJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;ValueTuple&lt;TLeftKey, TRightKey&gt;, TLeft, TRight, TDestination&gt;)</c>.</remarks>
    public static IObservable<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<TLeft, TRight, TDestination> resultSelector)
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
    /// <param name="left">The left <c>IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;</c> to join.</param>
    /// <param name="right">The right <c>IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;</c> to join.</param>
    /// <param name="rightKeySelector">A <c>Func&lt;T, TResult&gt;</c> that maps each right item to the left key it should join on.</param>
    /// <param name="resultSelector">A <c>Func&lt;T, TResult&gt;</c> that combines the composite key, left value, and right value into a destination object. Example: <c>((leftKey, rightKey), left, right) =&gt; new Result(leftKey, rightKey, left, right)</c>.</param>
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
    /// <seealso><c>LeftJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, TLeft, Optional&lt;TRight&gt;, TDestination&gt;)</c></seealso>
    /// <seealso><c>RightJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TRightKey, Optional&lt;TLeft&gt;, TRight, TDestination&gt;)</c></seealso>
    /// <seealso><c>FullJoin&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, Optional&lt;TLeft&gt;, Optional&lt;TRight&gt;, TDestination&gt;)</c></seealso>
    /// <seealso><c>InnerJoinMany&lt;TLeft, TLeftKey, TRight, TRightKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TLeft, TLeftKey&gt;&gt;, IObservable&lt;IChangeSet&lt;TRight, TRightKey&gt;&gt;, Func&lt;TRight, TLeftKey&gt;, Func&lt;TLeftKey, TLeft, IGrouping&lt;TRight, TRightKey, TLeftKey&gt;, TDestination&gt;)</c></seealso>
    public static IObservable<IChangeSet<TDestination, (TLeftKey leftKey, TRightKey rightKey)>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left, IObservable<IChangeSet<TRight, TRightKey>> right, Func<TRight, TLeftKey> rightKeySelector, Func<(TLeftKey leftKey, TRightKey rightKey), TLeft, TRight, TDestination> resultSelector)
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

        return new InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
    }
}
