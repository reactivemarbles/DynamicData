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
    /// Provides an overload of <c>TransformSafeAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="forceTransform">The forceTransform value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload takes a factory that receives only the current item.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return source.TransformSafeAsync((current, _, _) => transformFactory(current), errorHandler, forceTransform);
    }

    /// <summary>
    /// Provides an overload of <c>TransformSafeAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="forceTransform">The forceTransform value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload takes a factory that receives the current item and key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return source.TransformSafeAsync((current, _, key) => transformFactory(current, key), errorHandler, forceTransform);
    }

    /// <summary>
    /// Async version of <c>TransformSafe&lt;TDestination, TSource, TKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;, Func&lt;TSource, Optional&lt;TSource&gt;, TKey, TDestination&gt;, Action&lt;Error&lt;TSource, TKey&gt;&gt;, IObservable&lt;Func&lt;TSource, TKey, bool&gt;&gt;?)</c>.
    /// Projects each item using an async factory, catching factory exceptions via a mandatory error handler.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;</c> to transform asynchronously with error handling.</param>
    /// <param name="transformFactory">The <c>Func&lt;TSource, Optional&lt;TSource&gt;, TKey, Task&lt;TDestination&gt;&gt;</c> async function that produces a <typeparamref name="TDestination"/>.</param>
    /// <param name="errorHandler">A <c>Action&lt;T&gt;</c> that called when <paramref name="transformFactory"/> throws or faults. The item is skipped and the stream continues.</param>
    /// <param name="forceTransform">An optional <c>IObservable&lt;T&gt;</c> that forces re-transformation of matching items.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>Combines the async execution model of <c>TransformAsync&lt;TDestination, TSource, TKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;, Func&lt;TSource, Optional&lt;TSource&gt;, TKey, Task&lt;TDestination&gt;&gt;, IObservable&lt;Func&lt;TSource, TKey, bool&gt;&gt;?)</c> with the error-safe behavior of <c>TransformSafe&lt;TDestination, TSource, TKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;, Func&lt;TSource, Optional&lt;TSource&gt;, TKey, TDestination&gt;, Action&lt;Error&lt;TSource, TKey&gt;&gt;, IObservable&lt;Func&lt;TSource, TKey, bool&gt;&gt;?)</c>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, forceTransform).Run();
    }

    /// <summary>
    /// Provides an overload of <c>TransformSafeAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives only the current item.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return source.TransformSafeAsync((current, _, _) => transformFactory(current), errorHandler, options);
    }

    /// <summary>
    /// Provides an overload of <c>TransformSafeAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives the current item and key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return source.TransformSafeAsync((current, _, key) => transformFactory(current, key), errorHandler, options);
    }

    /// <summary>
    /// Provides an overload of <c>TransformSafeAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="transformFactory">The transformFactory value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, null, options.MaximumConcurrency, options.TransformOnRefresh).Run();
    }
}
