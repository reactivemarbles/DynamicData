// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Projects each item to a new form using a synchronous transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> to transform.</param>
    /// <param name="transformFactory">The <see cref="Func{T, TResult}"/> transform function applied to each item.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory and emit an update. When <see langword="false"/> (the default), Refresh is forwarded without re-transforming.</param>
    /// <returns>A list changeset stream of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Maintains an internal list of transformed items. Each source changeset is
    /// processed and a corresponding output changeset is produced with the transformed items.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The factory is called and an <b>Add</b> is emitted at the same index.</description></item>
    /// <item><term>AddRange</term><description>The factory is called for each item. An <b>AddRange</b> is emitted at the same start index.</description></item>
    /// <item><term>Replace</term><description>The factory is called for the new item. A <b>Replace</b> is emitted at the same index. The previous transformed value is available to overloads that accept <see cref="Optional{TDestination}"/>.</description></item>
    /// <item><term>Remove</term><description>A <b>Remove</b> is emitted (no factory call).</description></item>
    /// <item><term>RemoveRange</term><description>A <b>RemoveRange</b> is emitted.</description></item>
    /// <item><term>Moved</term><description>A <b>Moved</b> is emitted with updated indices (no factory call). Throws <see cref="UnspecifiedIndexException"/> if the source change has no index information.</description></item>
    /// <item><term>Refresh</term><description>If <paramref name="transformOnRefresh"/> is <see langword="false"/> (default), the <b>Refresh</b> is forwarded without re-transforming. If <see langword="true"/>, the factory is re-invoked and the result replaces the current value.</description></item>
    /// <item><term>Clear</term><description>A <b>Clear</b> is emitted and the internal list is emptied.</description></item>
    /// <item><term>OnError</term><description>If the factory throws, the exception propagates as OnError.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> By default, Refresh does NOT re-transform the item (it just forwards the signal). Set <paramref name="transformOnRefresh"/> to <see langword="true"/> if you need the factory re-invoked on Refresh. Add operations with out-of-bounds indices silently append to the end.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    /// <seealso cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <seealso cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="Convert{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination})"/>
    /// <seealso cref="ObservableCacheEx.Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, bool)"/>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return source.Transform<TSource, TDestination>((t, _, _) => transformFactory(t), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <summary>
    /// Projects each item using a transform function that also receives the item's index.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, int, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return source.Transform<TSource, TDestination>((t, _, idx) => transformFactory(t, idx), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <summary>
    /// Projects each item using a transform function that also receives the previously transformed value (if any).
    /// Type arguments must be specified explicitly as type inference fails for this overload.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, Optional<TDestination>, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return source.Transform<TSource, TDestination>((t, previous, _) => transformFactory(t, previous), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <summary>
    /// Projects each item using a transform function that receives the source item, the previously transformed value, and the index.
    /// Type arguments must be specified explicitly as type inference fails for this overload.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, Optional<TDestination>, int, TDestination> transformFactory, bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return new Transformer<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }
}
