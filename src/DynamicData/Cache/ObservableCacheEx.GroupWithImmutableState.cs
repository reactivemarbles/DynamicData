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
    /// <summary>
    /// Groups items by <paramref name="groupSelectorKey"/>, emitting immutable group snapshots instead of mutable sub-caches.
    /// Each group change contains a frozen copy of the group's state at that point in time.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group with immutable snapshots.</param>
    /// <param name="groupSelectorKey">A <see cref="Func{T, TResult}"/> that extracts the group key from each item.</param>
    /// <param name="regrouper">An <see cref="IObservable{Unit}"/> that optional signal to force re-evaluation of all items against the group selector.</param>
    /// <returns>An observable that emits immutable group changesets.</returns>
    /// <remarks>
    /// <para>
    /// Behaves identically to <see cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// in terms of how items are assigned to groups, but each group emission is an immutable snapshot.
    /// This makes it safe for parallel processing and eliminates race conditions on group state.
    /// The tradeoff is higher memory usage, since each change produces a new snapshot of the affected group.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item added to its group. An immutable snapshot of the group is emitted.</description></item>
    /// <item><term>Update</term><description>If group key unchanged, group snapshot re-emitted. If changed, item moves between groups; both affected groups emit new snapshots.</description></item>
    /// <item><term>Remove</term><description>Item removed from group. Updated snapshot emitted. Empty groups are removed.</description></item>
    /// <item><term>Refresh</term><description>Group key re-evaluated. If changed, item moves; affected group snapshots emitted.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Group{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TGroupKey})"/>
    /// <seealso cref="GroupOnPropertyWithImmutableState{TObject, TKey, TGroupKey}"/>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey, IObservable<Unit>? regrouper = null)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(groupSelectorKey);

        return new GroupOnImmutable<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
    }
}
