// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableList extensions for Transform, TransformAsync, and TransformMany.
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new Transformer<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }

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

    /// <summary>
    /// Flattens each source item into multiple destination items using <paramref name="manySelector"/>. Each source item produces zero or more children,
    /// all of which are merged into a single flat list changeset stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> to expand each item into multiple children.</param>
    /// <param name="manySelector">A <see cref="Func{T, TResult}"/> function that returns the child items for each source item.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TDestination}"/> used during Replace to determine which child items changed between old and new parent values.</param>
    /// <returns>A list changeset stream of all child items from all source items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="manySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Children expanded and added to the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old children diffed against new children (using <paramref name="equalityComparer"/>). Removed, added, or kept as appropriate.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>All children of the removed parents are removed from the output.</description></item>
    /// <item><term><b>Refresh</b></term><description>Children re-expanded and diffed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    /// <seealso cref="MergeManyChangeSets{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{TObject, IObservable{IChangeSet{TDestination}}}, IEqualityComparer{TDestination}?)"/>
    /// <seealso cref="ObservableCacheEx.TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IEnumerable<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
    }

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from an <see cref="ObservableCollection{T}"/>. The collection is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from a <see cref="ReadOnlyObservableCollection{T}"/>. The collection is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TSource}(IObservable{IChangeSet{TSource}}, Func{TSource, IEnumerable{TDestination}}, IEqualityComparer{TDestination}?)"/>
    /// <summary>
    /// Flattens each source item into children from an <see cref="IObservableList{T}"/>. The inner list is observed for subsequent changes.
    /// </summary>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IObservableList<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
}
