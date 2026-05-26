// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// ObservableCache extensions for the Transform family (Transform, TransformAsync, TransformSafe, TransformMany, and related overloads).
/// </summary>
public static partial class ObservableCacheEx
{
    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).AsObservableChangeSet(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).ToObservableChangeSet<TCollection, TDestination>().AddKey(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).Connect();

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a <c>bool transformOnRefresh</c> flag. When <see langword="true"/>, Refresh changes cause re-transformation (emitted as Update). The factory receives only the current item.</remarks>
    /// <seealso cref="ObservableListEx.Transform"/>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform((current, _, _) => transformFactory(current), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a <c>bool transformOnRefresh</c> flag. When <see langword="true"/>, Refresh changes cause re-transformation (emitted as Update). The factory receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform((current, _, key) => transformFactory(current, key), transformOnRefresh);
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a <c>bool transformOnRefresh</c> flag. When <see langword="true"/>, Refresh changes cause re-transformation (emitted as Update).</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, bool transformOnRefresh)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new Transform<TDestination, TSource, TKey>(source, transformFactory, transformOnRefresh: transformOnRefresh).Run();
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts an optional <c>forceTransform</c> predicate filtering by source item only (without the key). The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform((current, _, _) => transformFactory(current), forceTransform?.ForForced<TSource, TKey>());
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts an optional <c>forceTransform</c> predicate filtering by source item and key. The factory receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform((current, _, key) => transformFactory(current, key), forceTransform);
    }

    /// <summary>
    /// Projects each item in the changeset to a new form using a synchronous transform factory.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform.</param>
    /// <param name="transformFactory">The <see cref="Func{TSource, Optional{TSource}, TKey, TDestination}"/> that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
    /// <param name="forceTransform">An observable that, when it emits a predicate, re-transforms all items for which the predicate returns <see langword="true"/>. Re-transformed items are emitted as <see cref="ChangeReason.Update"/> changes. If <see langword="null"/>, no forced re-transforms occur.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Transform maintains a 1:1 mapping between source and destination items, keyed identically. The factory
    /// is called once per Add and once per Update. Removes are forwarded without calling the factory.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls factory, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls factory (receives current item, previous item, key), emits Update with Previous preserved.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove. Factory is NOT called.</description></item>
    ///   <item><term>Refresh</term><description>Forwarded as Refresh without re-transforming. To re-transform on Refresh, use the <paramref name="forceTransform"/> parameter or the <c>transformOnRefresh</c> overloads.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> By default, <b>Refresh</b> does NOT re-invoke the transform factory (it is just forwarded). Set <c>transformOnRefresh: true</c> to re-transform on <b>Refresh</b>.</para>
    /// <para>
    /// When <paramref name="forceTransform"/> emits a predicate, every cached item is tested against it.
    /// Matching items are re-transformed and emitted as Updates.
    /// </para>
    /// <para>
    /// Factory exceptions propagate as <see cref="IObserver{T}.OnError"/>, terminating the stream.
    /// Use <see cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// to catch factory errors without killing the stream.
    /// </para>
    /// </remarks>
    /// <seealso cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <seealso cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <seealso cref="TransformImmutable{TDestination, TSource, TKey}"/>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        if (forceTransform is not null)
        {
            return new TransformWithForcedTransform<TDestination, TSource, TKey>(source, transformFactory, forceTransform).Run();
        }

        return new Transform<TDestination, TSource, TKey>(source, transformFactory).Run();
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items when the observable emits. The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.Transform((cur, _, _) => transformFactory(cur), forceTransform.ForForced<TSource, TKey>());

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items when the observable emits. The factory receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

        return source.Transform((cur, _, key) => transformFactory(cur, key), forceTransform.ForForced<TSource, TKey>());
    }

    /// <inheritdoc cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items when the observable emits.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

        return source.Transform(transformFactory, forceTransform.ForForced<TSource, TKey>());
    }

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
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
    /// Async version of <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>.
    /// Projects each item using an async factory that returns <see cref="Task{TResult}"/>.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform asynchronously.</param>
    /// <param name="transformFactory">The <see cref="Func{TSource, Optional{TSource}, TKey, Task{TDestination}}"/> async function that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
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
    /// <see cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// to catch factory errors without terminating the stream.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, forceTransform).Run();
    }

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
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

    /// <inheritdoc cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, null, options.MaximumConcurrency, options.TransformOnRefresh).Run();
    }

    /// <summary>
    /// Optimized transform for immutable items with deterministic (pure) transform functions.
    /// Refresh changes are dropped entirely since immutable items cannot change in place.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform (items assumed immutable).</param>
    /// <param name="transformFactory">The <see cref="Func{TSource, TDestination}"/> pure function that maps a source item to a destination item. Must be deterministic: same input always produces equivalent output.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Because the transform is assumed to be stateless and deterministic, this operator does not track
    /// previously transformed items. This reduces memory overhead compared to <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls factory, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls factory, emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove. Factory is NOT called.</description></item>
    ///   <item><term>Refresh</term><description>DROPPED. Immutable items do not change, so Refresh is meaningless.</description></item>
    /// </list>
    /// <para>Use this when items are immutable, the factory is pure, and the factory is cheap. If any of these conditions are false, use <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/> instead.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformImmutable<TDestination, TSource, TKey>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TDestination> transformFactory)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformImmutable<TDestination, TSource, TKey>(
                source: source,
                transformFactory: transformFactory)
            .Run();
    }

    /// <summary>
    /// Flattens each source item into zero or more destination items (1:N), producing a single flat changeset.
    /// Each child item must have a globally unique key across all parents.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TSourceKey}}"/> to expand each item into multiple children.</param>
    /// <param name="manySelector">A function that expands a parent item into its children. For <see cref="ObservableCollection{T}"/> or <see cref="IObservableCache{TObject, TKey}"/> overloads, subsequent changes to the child collection are automatically tracked.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that extracts a unique key from each child item. Keys must be unique across ALL parents, not just within one parent.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="manySelector"/>, emits Add for each child.</description></item>
    ///   <item><term>Update</term><description>Diffs old children vs new children: emits Remove for removed children, Add for new children, Update for children with matching keys.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove for all children of the removed parent.</description></item>
    ///   <item><term>Refresh</term><description>Propagated as Refresh to all children (no re-expansion).</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> If two source items produce children with the same key, last-in-wins. <b>Refresh</b> does NOT re-expand children (only <b>Update</b> does).</para>
    /// <para>If two parents produce children with the same key, last-in-wins. Use the async variant with a <see cref="IComparer{T}"/> to control conflict resolution.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <seealso cref="ObservableListEx.TransformMany"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="ObservableCollection{T}"/> selector. Changes to the child collection (adds, removes, replacements) are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts a <see cref="ReadOnlyObservableCollection{T}"/> selector. Changes to the child collection are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="IObservableCache{TObject, TKey}"/> selector. The child cache is live: subsequent changes to it are automatically propagated downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IObservableCache<TDestination, TDestinationKey>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Async version of <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>.
    /// Flattens each source item into zero or more destination items using an async factory.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TSourceKey}}"/> to expand each item into multiple children asynchronously.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <see cref="IEnumerable{T}"/> of children.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that extracts a unique key from each child item.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional comparer to determine if two child items with the same key are equal. Used to suppress no-op updates.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents. The winning item is determined by this comparer.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para>
    /// Because each parent's expansion is async, child collections may arrive via separate changesets
    /// (unlike the synchronous <c>TransformMany</c> which batches all children into one changeset).
    /// </para>
    /// <para>
    /// Factory exceptions propagate as <see cref="IObserver{T}.OnError"/>. Use
    /// <see cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// to catch errors without killing the stream.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="manySelector"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live: its changes propagate downstream. No <c>keySelector</c> is needed since the cache already has keys. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), equalityComparer, comparer);

    /// <summary>
    /// Async version of <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// with error handling. Factory exceptions are caught and routed to <paramref name="errorHandler"/> instead of
    /// terminating the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TSourceKey}}"/> to expand each item into multiple children asynchronously with error handling.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <see cref="IEnumerable{T}"/> of children.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that extracts a unique key from each child item.</param>
    /// <param name="errorHandler">A <see cref="Action{T}"/> that called when <paramref name="manySelector"/> throws. The faulting item is skipped and the stream continues.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional comparer to determine if two child items with the same key are equal.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>Because the transformations are asynchronous, each sub-collection may be emitted via a separate changeset.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), errorHandler, equalityComparer, comparer);

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return new TransformOnObservable<TSource, TKey, TDestination>(source, transformFactory).Run();
    }

    /// <inheritdoc cref="TransformOnObservable{TSource, TKey, TDestination}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TKey, IObservable{TDestination}})"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformOnObservable<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, IObservable<TDestination>> transformFactory)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.TransformOnObservable((obj, _) => transformFactory(obj));
    }

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a simpler factory that receives only the current item, and a forceTransform predicate filtering by source item only.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafe((current, _, _) => transformFactory(current), errorHandler, forceTransform.ForForced<TSource, TKey>());
    }

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts a factory that receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafe((current, _, key) => transformFactory(current, key), errorHandler, forceTransform);
    }

    /// <summary>
    /// Projects each item using a synchronous factory, catching factory exceptions via a mandatory error handler
    /// instead of terminating the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform with error handling.</param>
    /// <param name="transformFactory">The <see cref="Func{TSource, Optional{TSource}, TKey, TDestination}"/> that produces a <typeparamref name="TDestination"/> from the current source item, the previous source item (if any), and the key.</param>
    /// <param name="errorHandler">A callback invoked when <paramref name="transformFactory"/> throws. Receives an <see cref="Error{TSource, TKey}"/> containing the exception and the faulting item. The item is skipped and the stream continues.</param>
    /// <param name="forceTransform">An optional <see cref="IObservable{T}"/> that, when it emits a predicate, re-transforms all items for which the predicate returns <see langword="true"/>. If <see langword="null"/>, no forced re-transforms occur.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Behaves identically to <see cref="Transform{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// except that factory exceptions are routed to <paramref name="errorHandler"/> instead of propagating as <see cref="IObserver{T}.OnError"/>.
    /// Source-level errors (i.e. the source observable itself erroring) still propagate normally.
    /// </para>
    /// <para><b>Worth noting:</b> Factory exceptions are caught per-item; the faulting item is skipped and reported to the error handler while the stream continues. Source-level errors still terminate the stream.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));
        if (forceTransform is not null)
        {
            return new TransformWithForcedTransform<TDestination, TSource, TKey>(source, transformFactory, forceTransform, errorHandler).Run();
        }

        return new Transform<TDestination, TSource, TKey>(source, transformFactory, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items. The factory receives only the current item.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.TransformSafe((cur, _, _) => transformFactory(cur), errorHandler, forceTransform.ForForced<TSource, TKey>());

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items. The factory receives the current item and key.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

        return source.TransformSafe((cur, _, key) => transformFactory(cur, key), errorHandler, forceTransform.ForForced<TSource, TKey>());
    }

    /// <inheritdoc cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="IObservable{T}"/> of <see cref="Unit"/> to force re-transformation of ALL items.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

        return source.TransformSafe(transformFactory, errorHandler, forceTransform.ForForced<TSource, TKey>());
    }

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a factory that receives only the current item.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafeAsync((current, _, _) => transformFactory(current), errorHandler, forceTransform);
    }

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload takes a factory that receives the current item and key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafeAsync((current, _, key) => transformFactory(current, key), errorHandler, forceTransform);
    }

    /// <summary>
    /// Async version of <see cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>.
    /// Projects each item using an async factory, catching factory exceptions via a mandatory error handler.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform asynchronously with error handling.</param>
    /// <param name="transformFactory">The <see cref="Func{TSource, Optional{TSource}, TKey, Task{TDestination}}"/> async function that produces a <typeparamref name="TDestination"/>.</param>
    /// <param name="errorHandler">A <see cref="Action{T}"/> that called when <paramref name="transformFactory"/> throws or faults. The item is skipped and the stream continues.</param>
    /// <param name="forceTransform">An optional <see cref="IObservable{T}"/> that forces re-transformation of matching items.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>Combines the async execution model of <see cref="TransformAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, IObservable{Func{TSource, TKey, bool}}?)"/> with the error-safe behavior of <see cref="TransformSafe{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, TDestination}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, forceTransform).Run();
    }

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives only the current item.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafeAsync((current, _, _) => transformFactory(current), errorHandler, options);
    }

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling. The factory receives the current item and key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafeAsync((current, _, key) => transformFactory(current, key), errorHandler, options);
    }

    /// <inheritdoc cref="TransformSafeAsync{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, Optional{TSource}, TKey, Task{TDestination}}, Action{Error{TSource, TKey}}, IObservable{Func{TSource, TKey, bool}}?)"/>
    /// <remarks>This overload accepts <see cref="TransformAsyncOptions"/> to control concurrency and Refresh handling.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory, Action<Error<TSource, TKey>> errorHandler, TransformAsyncOptions options)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, null, options.MaximumConcurrency, options.TransformOnRefresh).Run();
    }

    /// <summary>
    /// Builds a hierarchical tree from a flat changeset using a parent key selector.
    /// Each item becomes a <see cref="Node{TObject, TKey}"/> with Parent, Children, Depth, and IsRoot properties.
    /// </summary>
    /// <typeparam name="TObject">The type of the source items. Must be a reference type.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to transform into a hierarchical tree.</param>
    /// <param name="pivotOn">The <see cref="Func{TObject, TKey}"/> that returns the key of an item's parent. Return the item's own key (or a non-existent key) for root items.</param>
    /// <param name="predicateChanged">An optional <see cref="IObservable{T}"/> that emits a filter predicate for nodes. When the predicate changes, nodes are re-evaluated and filtered.</param>
    /// <returns>An observable changeset of <see cref="Node{TObject, TKey}"/> items representing the tree.</returns>
    /// <remarks>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Creates node, attaches to parent (or root if parent not found), emits Add.</description></item>
    ///   <item><term>Update</term><description>Updates node. If <paramref name="pivotOn"/> returns a different parent key, the node is re-parented.</description></item>
    ///   <item><term>Remove</term><description>Removes node. Orphaned children become root nodes.</description></item>
    ///   <item><term>Refresh</term><description>Re-evaluates parent key. May re-parent the node if the parent changed.</description></item>
    /// </list>
    /// <para>Circular references are NOT detected. If item A is the parent of B and B is the parent of A, behavior is undefined.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="pivotOn"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> TransformToTree<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey> pivotOn, IObservable<Func<Node<TObject, TKey>, bool>>? predicateChanged = null)
        where TObject : class
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pivotOn.ThrowArgumentNullExceptionIfNull(nameof(pivotOn));

        return new TreeBuilder<TObject, TKey>(source, pivotOn, predicateChanged).Run();
    }

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload defaults to <c>transformOnRefresh: false</c> and does not provide an error handler (factory exceptions propagate as OnError).</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return source.TransformWithInlineUpdate(transformFactory, updateAction, false);
    }

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload does not provide an error handler (factory exceptions propagate as OnError). The <c>transformOnRefresh</c> parameter controls Refresh behavior.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, bool transformOnRefresh)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return new TransformWithInlineUpdate<TDestination, TSource, TKey>(source, transformFactory, updateAction, transformOnRefresh: transformOnRefresh).Run();
    }

    /// <inheritdoc cref="TransformWithInlineUpdate{TDestination, TSource, TKey}(IObservable{IChangeSet{TSource, TKey}}, Func{TSource, TDestination}, Action{TDestination, TSource}, Action{Error{TSource, TKey}}, bool)"/>
    /// <remarks>This overload defaults to <c>transformOnRefresh: false</c> but includes an error handler for factory/update action exceptions.</remarks>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, Action<Error<TSource, TKey>> errorHandler)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformWithInlineUpdate(transformFactory, updateAction, errorHandler, false);
    }

    /// <summary>
    /// Projects each item using a transform factory for Add, and mutates the existing transformed
    /// item in place (via an update action) for Update, preserving the original object reference.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items. Must be a reference type since items are mutated in place.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to transform with in-place mutation on updates.</param>
    /// <param name="transformFactory">A <see cref="Func{T, TResult}"/> that called on Add (and optionally Refresh) to create a new <typeparamref name="TDestination"/>.</param>
    /// <param name="updateAction">A <see cref="Action{T}"/> that called on Update. Receives <c>(existingTransformed, newSource)</c>. Mutate the existing transformed item to reflect the new source value. Example: <c>(vm, model) =&gt; vm.Value = model.Value</c>.</param>
    /// <param name="errorHandler">A <see cref="Action{T}"/> that called when <paramref name="transformFactory"/> or <paramref name="updateAction"/> throws. The faulting item is skipped.</param>
    /// <param name="transformOnRefresh">When <see langword="true"/>, Refresh changes call <paramref name="updateAction"/> on the existing item.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// This is useful when the destination type is a ViewModel that should maintain its identity across updates.
    /// Instead of replacing the entire ViewModel, the update action patches the existing instance.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="transformFactory"/>, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls <paramref name="updateAction"/> on the EXISTING transformed item (same reference), emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove.</description></item>
    ///   <item><term>Refresh</term><description>If <paramref name="transformOnRefresh"/> is true, calls <paramref name="updateAction"/>. Otherwise forwarded as Refresh.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="transformFactory"/>, <paramref name="updateAction"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformWithInlineUpdate<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<TDestination, TSource> updateAction, Action<Error<TSource, TKey>> errorHandler, bool transformOnRefresh)
        where TDestination : class
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformWithInlineUpdate<TDestination, TSource, TKey>(source, transformFactory, updateAction, errorHandler, transformOnRefresh).Run();
    }
}
