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
    /// Provides an overload of <c>TransformSafe</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="forceTransform">The forceTransform value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts a simpler factory that receives only the current item, and a forceTransform predicate filtering by source item only.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return source.TransformSafe((current, _, _) => transformFactory(current), errorHandler, forceTransform.ForForced<TSource, TKey>());
    }

    /// <summary>
    /// Provides an overload of <c>TransformSafe</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="forceTransform">The forceTransform value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts a factory that receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return source.TransformSafe((current, _, key) => transformFactory(current, key), errorHandler, forceTransform);
    }

    /// <summary>
    /// Projects each item using a synchronous factory, catching factory exceptions via a mandatory error handler
    /// instead of terminating the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;</c> to transform with error handling.</param>
    /// <param name="transformFactory">The <c>Func&lt;TSource, Optional&lt;TSource&gt;, TKey, TDestination&gt;</c> that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
    /// <param name="errorHandler">A callback invoked when <paramref name="transformFactory"/> throws. Receives an <c>Error&lt;TSource, TKey&gt;</c> containing the exception and the faulting item. The item is skipped and the stream continues.</param>
    /// <param name="forceTransform">An optional <c>IObservable&lt;T&gt;</c> that, when it emits a predicate, re-transforms all items for which the predicate returns <see langword="true"/>. If <see langword="null"/>, no forced re-transforms occur.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Behaves identically to <c>Transform&lt;TDestination, TSource, TKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;, Func&lt;TSource, Optional&lt;TSource&gt;, TKey, TDestination&gt;, IObservable&lt;Func&lt;TSource, TKey, bool&gt;&gt;?)</c>
    /// except that factory exceptions are routed to <paramref name="errorHandler"/> instead of propagating as <c>IObserver&lt;T&gt;.OnError</c>.
    /// Source-level errors (i.e. the source observable itself erroring) still propagate normally.
    /// </para>
    /// <para><b>Worth noting:</b> Factory exceptions are caught per-item; the faulting item is skipped and reported to the error handler while the stream continues. Source-level errors still terminate the stream.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);
        if (forceTransform is not null)
        {
            return new TransformWithForcedTransform<TDestination, TSource, TKey>(source, transformFactory, forceTransform, errorHandler).Run();
        }

        return new Transform<TDestination, TSource, TKey>(source, transformFactory, errorHandler).Run();
    }

    /// <summary>
    /// Provides an overload of <c>ForForced</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="forceTransform">The forceTransform value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts <c>IObservable&lt;T&gt;</c> of <see cref="Unit"/> to force re-transformation of ALL items. The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.TransformSafe((cur, _, _) => transformFactory(cur), errorHandler, forceTransform.ForForced<TSource, TKey>());

    /// <summary>
    /// Provides an overload of <c>TransformSafe</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="forceTransform">The forceTransform value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts <c>IObservable&lt;T&gt;</c> of <see cref="Unit"/> to force re-transformation of ALL items. The factory receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(forceTransform);

        return source.TransformSafe((cur, _, key) => transformFactory(cur, key), errorHandler, forceTransform.ForForced<TSource, TKey>());
    }

    /// <summary>
    /// Provides an overload of <c>TransformSafe</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="forceTransform">The forceTransform value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts <c>IObservable&lt;T&gt;</c> of <see cref="Unit"/> to force re-transformation of ALL items.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(forceTransform);

        return source.TransformSafe(transformFactory, errorHandler, forceTransform.ForForced<TSource, TKey>());
    }
}
