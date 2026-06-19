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
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Kernel.Optional<TSource>, TKey, TDestination> transformFactory, bool transformOnRefresh)
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
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Kernel.Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
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
    public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Kernel.Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

        return source.Transform(transformFactory, forceTransform.ForForced<TSource, TKey>());
    }
}
