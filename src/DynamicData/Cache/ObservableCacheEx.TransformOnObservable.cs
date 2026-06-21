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
    /// Projects each item into a per-item observable. The latest value emitted by each item's observable
    /// becomes the transformed value in the output changeset.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform using per-item observables.</param>
    /// <param name="transformFactory">A function that, given a source item and its key, returns an <see cref="IObservable{TDestination}"/> whose emissions become the transformed values.</param>
    /// <returns>An observable changeset where each key's value is the latest emission from its per-item observable.</returns>
    /// <remarks>
    /// <para>
    /// <b>Source changeset handling (parent events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="transformFactory"/> and subscribes to the returned observable. The item is <b>not visible downstream until the observable emits its first value</b>.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's observable subscription and subscribes to the new item's observable. The item disappears from downstream until the new observable emits.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's observable subscription. If the item was visible downstream, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the item is currently visible downstream. Otherwise dropped.</description></item>
    /// </list>
    /// <para>
    /// <b>Per-item observable handling (transform observable events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Emission</term><description>Behavior</description></listheader>
    /// <item><term>First value</term><description>The transformed item appears downstream as an <b>Add</b>.</description></item>
    /// <item><term>Subsequent values</term><description>Each new value replaces the previous one: an <b>Update</b> is emitted downstream.</description></item>
    /// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
    /// <item><term>Completed</term><description>The item remains at its last emitted value. No further updates are possible for this item.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> Items are invisible downstream until their per-item observable emits at least one value.
    /// If an item's observable never emits, that item never appears in the output. The transform factory's selector
    /// runs under an internal lock, so it must not synchronously access other DynamicData caches (deadlock risk in
    /// cross-cache pipelines). The output completes when the source completes and all per-item observables have
    /// also completed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TKey, TDestination}, bool)"/>
    /// <seealso cref="FilterOnObservable{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{bool}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupOnObservable{TObject, TKey, TGroupKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TKey, IObservable{TGroupKey}})"/>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformOnObservable<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transformFactory)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return new TransformOnObservable<TSource, TKey, TDestination>(source, transformFactory).Run();
    }

    /// <inheritdoc cref="TransformOnObservable{TSource, TKey, TDestination}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TKey, IObservable{TDestination}})"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformOnObservable<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, IObservable<TDestination>> transformFactory)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return source.TransformOnObservable((obj, _) => transformFactory(obj));
    }
}
