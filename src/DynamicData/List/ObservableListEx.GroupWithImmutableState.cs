// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
#if REACTIVE_SHIM
using DynamicData.Reactive.List.Internal;
#else
using DynamicData.List.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Groups source items by the value returned by <paramref name="groupSelectorKey"/>. Each update produces immutable grouping snapshots
    /// rather than live inner observable lists.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to group with immutable snapshots.</param>
    /// <param name="groupSelectorKey">A <c>Func&lt;T, TResult&gt;</c> function that returns the group key for each item.</param>
    /// <param name="regrouper">An optional <c>IObservable&lt;Unit&gt;</c> of <see cref="Unit"/> that forces all items to be re-evaluated when it fires.</param>
    /// <returns>A list changeset stream of <c>List.IGrouping&lt;TObject, TGroupKey&gt;</c> immutable snapshots.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="groupSelectorKey"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Works like <c>GroupOn&lt;TObject, TGroup&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, TGroup&gt;, IObservable&lt;Unit&gt;?)</c>
    /// but each affected group emits a new immutable snapshot on every change rather than updating a live inner list.
    /// This is useful when consumers need thread-safe, point-in-time snapshots of each group.
    /// </para>
    /// </remarks>
    /// <seealso><c>GroupOn&lt;TObject, TGroup&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, TGroup&gt;, IObservable&lt;Unit&gt;?)</c></seealso>
    /// <seealso><c>GroupOnPropertyWithImmutableState&lt;TObject, TGroup&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TGroup&gt;&gt;, TimeSpan?, IScheduler?)</c></seealso>
    public static IObservable<IChangeSet<List.IGrouping<TObject, TGroupKey>>> GroupWithImmutableState<TObject, TGroupKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        ArgumentExceptionHelper.ThrowIfNull(groupSelectorKey);

        return new GroupOnImmutable<TObject, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }
}
