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
    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Kernel.Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a simpler factory that receives only the current item.</remarks>
    /// <seealso cref="ObservableListEx.TransformAsync"/>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync((current, _, _) => transformFactory(current), forceTransform);
    }

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Kernel.Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a factory that receives the current item and key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync((current, _, key) => transformFactory(current, key), forceTransform);
    }

    /// <summary>
    /// Async version of <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Kernel.Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>.
    /// Projects each item using an async factory that returns <see cref="Task{TResult}"/>.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform asynchronously.</param>
    /// <param name="transformFactory">The <see cref="Func{TSource, Kernel.Optional{TSource}, TKey, Task{TDestination}}"/> async function that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
    /// <param name="forceTransform">An observable that, when it emits a predicate, re-transforms all items for which the predicate returns <see langword="true"/>. Re-transformed items are emitted as <see cref="ChangeReason.Update"/> changes. If <see langword="null"/>, no forced re-transforms occur.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Transforms within a single changeset batch execute concurrently. The entire batch must complete
    /// before the resulting changeset is emitted. Use the <see cref="TransformAsyncOptions"/> overloads
    /// to control maximum concurrency and Refresh handling.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Awaits factory, emits Add.</description></item>
    ///   <item><term>Update</term><description>Awaits factory (receives current, previous, key), emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove. Factory is NOT called.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh by default. Use <see cref="TransformAsyncOptions.TransformOnRefresh"/> to re-transform.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Transforms are batched per changeset (all tasks must complete before the next changeset is processed). Completion waits for in-flight transforms. <b>Remove</b> does NOT cancel in-flight transforms for the removed key.</para>
    /// <para>
    /// Factory exceptions propagate as <see cref="IObserver{T}.OnError"/>. Use
    /// <see cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Kernel.Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// to catch factory errors without terminating the stream.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Kernel.Optional<TSource>, TKey, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, forceTransform).Run();
    }

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Kernel.Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives only the current item.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync((current, _, _) => transformFactory(current), options);
    }

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Kernel.Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives the current item and key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformAsync((current, _, key) => transformFactory(current, key), options);
    }

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Kernel.Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Kernel.Optional<TSource>, TKey, Task<TDestination>> transformFactory, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, null, options.MaximumConcurrency, options.TransformOnRefresh).Run();
    }
}
