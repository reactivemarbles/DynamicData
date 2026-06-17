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
    /// Projects each item to a new form using an async transform function. Behaves like <see cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/> but the factory returns a <see cref="Task{T}"/>.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> to transform asynchronously.</param>
    /// <param name="transformFactory">An <see cref="Func{T, TResult}"/> async function that transforms each source item.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory.</param>
    /// <returns>A list changeset stream of asynchronously transformed items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Change handling is identical to the synchronous <see cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/> except the factory is awaited. Operations are serialized per changeset via a semaphore.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/AddRange</term><description>The async factory is awaited for each item. An <b>Add</b>/<b>AddRange</b> is emitted with the transformed results.</description></item>
    /// <item><term>Replace</term><description>The async factory is awaited for the new item. A <b>Replace</b> is emitted.</description></item>
    /// <item><term>Remove/RemoveRange</term><description>Emitted without invoking the factory.</description></item>
    /// <item><term>Moved</term><description>Emitted with updated indices (no factory call).</description></item>
    /// <item><term>Refresh</term><description>If <paramref name="transformOnRefresh"/> is <see langword="false"/> (default), forwarded without re-transforming. If <see langword="true"/>, the factory is re-awaited.</description></item>
    /// <item><term>Clear</term><description>Emitted and internal list cleared.</description></item>
    /// <item><term>OnError</term><description>If the async factory throws, the exception propagates as OnError.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded after the last changeset is processed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> All async transforms within a single changeset are serialized (not parallel). Each changeset is fully processed before the next begins. By default, Refresh does NOT re-transform.</para>
    /// </remarks>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <seealso cref="ObservableCacheEx.TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}})"/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, _, _) => transformFactory(t), transformOnRefresh);
    }

    /// <inheritdoc cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <summary>
    /// Async transform overload receiving the source item and its index.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, _, i) => transformFactory(t, i), transformOnRefresh);
    }

    /// <inheritdoc cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <summary>
    /// Async transform overload receiving the source item and the previously transformed value.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync<TSource, TDestination>((t, d, _) => transformFactory(t, d), transformOnRefresh);
    }

    /// <inheritdoc cref="TransformAsync{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, Task{TDestination}}, bool)"/>
    /// <summary>
    /// Async transform overload receiving the source item, previously transformed value, and index. This is the terminal overload that all other TransformAsync overloads delegate to.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Optional<TDestination>, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }
}
