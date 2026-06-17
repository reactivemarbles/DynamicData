// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Groups source items by the value returned by <paramref name="groupSelector"/>. Each group is an <see cref="IGroup{TObject, TGroup}"/>
    /// containing an inner observable list of its members.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TGroup">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to group.</param>
    /// <param name="groupSelector">A <see cref="Func{T, TResult}"/> function that returns the group key for each item.</param>
    /// <param name="regrouper">An optional <see cref="IObservable{Unit}"/> of <see cref="Unit"/> that forces all items to be re-evaluated against <paramref name="groupSelector"/> when it fires. Useful for time-based groupings (e.g., "Last Hour", "Today").</param>
    /// <returns>A list changeset stream of <see cref="IGroup{TObject, TGroup}"/> objects, each containing the items belonging to that group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="groupSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Groups are created lazily and removed when empty. Each group exposes an inner observable list that receives incremental updates.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Group key evaluated. Item added to its group. If the group is new, an <b>Add</b> of the group is emitted.</description></item>
    /// <item><term><b>Replace</b></term><description>Group key re-evaluated. If the group changed, the item is removed from the old group and added to the new one. Empty old groups are removed.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Item removed from its group. Empty groups are removed from the result.</description></item>
    /// <item><term><b>Refresh</b></term><description>Group key re-evaluated. If changed, the item moves between groups.</description></item>
    /// <item><term><b>Moved</b></term><description>Not handled by group logic.</description></item>
    /// <item><term>Regrouper fires</term><description>All items re-evaluated. Items that changed group key are moved between groups. Empty groups removed, new groups added.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="GroupOnProperty{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupWithImmutableState{TObject, TGroupKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroupKey}, IObservable{Unit}?)"/>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOn<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroup> groupSelector, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TGroup : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        groupSelector.ThrowArgumentNullExceptionIfNull(nameof(groupSelector));

        return new GroupOn<TObject, TGroup>(source, groupSelector, regrouper).Run();
    }
}
