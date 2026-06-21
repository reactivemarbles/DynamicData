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
    /// Filters and casts items in the changeset to <typeparamref name="TDestination"/>. Items that are not of type
    /// <typeparamref name="TDestination"/> are excluded. Combines filter and transform in one step without an intermediate cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects in the source changeset.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The destination type to filter and cast to.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter by type.</param>
    /// <param name="suppressEmptyChangeSets">If <see langword="true"/>, changesets that become empty after filtering are suppressed.</param>
    /// <returns>An observable changeset of <typeparamref name="TDestination"/> items.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>If the item is <typeparamref name="TDestination"/>, cast and emit as <b>Add</b>. Otherwise dropped.</description></item>
    ///   <item><term><b>Update</b></term><description>Re-evaluated. If the new item is <typeparamref name="TDestination"/>, emit accordingly. If the old item was downstream but the new one is not, emit <b>Remove</b>.</description></item>
    ///   <item><term><b>Remove</b></term><description>If the item was downstream, emit <b>Remove</b>.</description></item>
    ///   <item><term><b>Refresh</b></term><description>If the item is downstream, forwarded as <b>Refresh</b>.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> OfType<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new OfType<TObject, TKey, TDestination>(source, suppressEmptyChangeSets).Run();
    }
}
