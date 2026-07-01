// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    /// Projects each item to a new form using an async transform function. Behaves like <c>Transform&lt;TSource, TDestination&gt;(IObservable&lt;IChangeSet&lt;TSource&gt;&gt;, Func&lt;TSource, TDestination&gt;, bool)</c> but the factory returns a <c>Task&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource&gt;&gt;</c> to transform asynchronously.</param>
    /// <param name="transformFactory">An <c>Func&lt;T, TResult&gt;</c> async function that transforms each source item.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh events re-invoke the factory.</param>
    /// <returns>A list changeset stream of asynchronously transformed items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Change handling is identical to the synchronous <c>Transform&lt;TSource, TDestination&gt;(IObservable&lt;IChangeSet&lt;TSource&gt;&gt;, Func&lt;TSource, TDestination&gt;, bool)</c> except the factory is awaited. Operations are serialized per changeset via a semaphore.</para>
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
    /// <seealso><c>Transform&lt;TSource, TDestination&gt;(IObservable&lt;IChangeSet&lt;TSource&gt;&gt;, Func&lt;TSource, TDestination&gt;, bool)</c></seealso>
    /// <seealso><c>ObservableCacheEx.TransformAsync&lt;TDestination, TSource, TKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;, Func&lt;TSource, Task&lt;TDestination&gt;&gt;, IObservable&lt;Func&lt;TSource, TKey, bool&gt;&gt;)</c></seealso>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return source.TransformAsync<TSource, TDestination>((t, _, _) => transformFactory(t), transformOnRefresh);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Async transform overload receiving the source item and its index.
    /// </summary>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="transformOnRefresh">The transformOnRefresh value.</param>
    /// <returns>The resulting observable sequence.</returns>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return source.TransformAsync<TSource, TDestination>((t, _, i) => transformFactory(t, i), transformOnRefresh);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Async transform overload receiving the source item and the previously transformed value.
    /// </summary>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="transformOnRefresh">The transformOnRefresh value.</param>
    /// <returns>The resulting observable sequence.</returns>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, ReactiveUI.Primitives.Optional<TDestination>, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return source.TransformAsync<TSource, TDestination>((t, d, _) => transformFactory(t, d), transformOnRefresh);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Async transform overload receiving the source item, previously transformed value, and index. This is the terminal overload that all other TransformAsync overloads delegate to.
    /// </summary>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="transformOnRefresh">The transformOnRefresh value.</param>
    /// <returns>The resulting observable sequence.</returns>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
        this IObservable<IChangeSet<TSource>> source,
        Func<TSource, ReactiveUI.Primitives.Optional<TDestination>, int, Task<TDestination>> transformFactory,
        bool transformOnRefresh = false)
        where TSource : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return new TransformAsync<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
    }
}
