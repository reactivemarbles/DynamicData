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
    /// Creates a filtered stream, optimized for stateless/deterministic filtering of immutable items.
    /// </summary>
    /// <typeparam name="TObject">The type of collection items to be filtered.</typeparam>
    /// <typeparam name="TKey">The type of the key values of each collection item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter (items assumed immutable).</param>
    /// <param name="predicate">The <see cref="Func{TObject, bool}"/> filtering predicate to be applied to each item.</param>
    /// <param name="suppressEmptyChangeSets">A flag indicating whether the created stream should emit empty changesets. Empty changesets are suppressed by default, for performance. Set to ensure that a downstream changeset occurs for every upstream changeset.</param>
    /// <returns>A stream of collection changesets where upstream collection items are filtered by the given predicate.</returns>
    /// <remarks>
    /// <para>The goal of this operator is to optimize a common use-case of reactive programming, where data values flowing through a stream are immutable, and state changes are distributed by publishing new immutable items as replacements, instead of mutating the items directly.</para>
    /// <para>In addition to assuming that all collection items are immutable, this operator also assumes that the given filter predicate is deterministic, such that the result it returns will always be the same each time a specific input is passed to it. In other words, the predicate itself also contains no mutable state.</para>
    /// <para>Under these assumptions, this operator can bypass the need to keep track of every collection item that passes through it, which the normal <see cref="Filter{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, bool}, bool)"/> operator must do, in order to re-evaluate the filtering status of items, during a refresh operation.</para>
    /// <para>Consider using this operator when the following are true:</para>
    /// <list type="bullet">
    /// <item><description>Your collection items are immutable, and changes are published by replacing entire items</description></item>
    /// <item><description>Your filtering logic does not change over the lifetime of the stream, only the items do</description></item>
    /// <item><description>Your filtering predicate runs quickly, and does not heavily allocate memory</description></item>
    /// </list>
    /// <para>Note that, because filtering is purely deterministic, Refresh operations are transparently ignored by this operator.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The predicate is evaluated. If it passes, an <b>Add</b> is emitted. Otherwise the item is dropped.</description></item>
    /// <item><term>Update</term><description>Four outcomes: if both old and new values pass, an <b>Update</b> is emitted. If only the new value passes, an <b>Add</b> is emitted. If only the old value passed, a <b>Remove</b> is emitted. If neither passes, the change is dropped.</description></item>
    /// <item><term>Remove</term><description>If the item was included downstream, a <b>Remove</b> is emitted. Otherwise dropped.</description></item>
    /// <item><term>Refresh</term><description><b>Dropped.</b> Because items are assumed immutable, there is nothing to re-evaluate.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> FilterImmutable<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, bool> predicate,
            bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(predicate);

        return new FilterImmutable<TObject, TKey>(
                predicate: predicate,
                source: source,
                suppressEmptyChangeSets: suppressEmptyChangeSets)
            .Run();
    }
}
