// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;

using DynamicData.Kernel;

namespace DynamicData.Alias;

/// <summary>
/// Observable cache alias names.
/// </summary>
public static class ObservableCacheAlias
{
    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="forceTransform">Invoke to force a new transform for all items.</param>#
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

        return source.Transform(transformFactory, forceTransform);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.Transform(transformFactory, forceTransform);

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="forceTransform">Invoke to force a new transform for all items.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.Transform(transformFactory, forceTransform);

    /// <summary>
    /// Projects each update item to a new form using the specified transform function.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));

        return source.Transform(transformFactory, forceTransform);
    }

    /// <summary>
    /// Equivalent to a select many transform. To work, the key must individually identify each child.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="manySelector">The selector for selecting the enumerable.</param>
    /// <param name="keySelector">The key selector which must be unique across all.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> SelectMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformMany(manySelector, keySelector);

    /// <summary>
    /// Projects each update item to a new form using the specified transform function,
    /// providing an error handling action to safely handle transform errors without killing the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
    ///  If not specified the stream will terminate as per rx convention.
    /// </param>
    /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));
        forceTransform.ThrowArgumentNullExceptionIfNull(nameof(forceTransform));

        return source.TransformSafe(transformFactory, errorHandler, forceTransform);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function,
    /// providing an error handling action to safely handle transform errors without killing the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
    ///  If not specified the stream will terminate as per rx convention.
    /// </param>
    /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafe(transformFactory, errorHandler, forceTransform);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function,
    /// providing an error handling action to safely handle transform errors without killing the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
    ///  If not specified the stream will terminate as per rx convention.
    /// </param>
    /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>>? forceTransform = null)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformFactory.ThrowArgumentNullExceptionIfNull(nameof(transformFactory));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return source.TransformSafe(transformFactory, errorHandler, forceTransform);
    }

    /// <summary>
    /// Projects each update item to a new form using the specified transform function,
    /// providing an error handling action to safely handle transform errors without killing the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformFactory">The transform factory.</param>
    /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
    ///  If not specified the stream will terminate as per rx convention.
    /// </param>
    /// <param name="forceTransform">Invoke to force a new transform for all items.</param>
    /// <returns>
    /// A transformed update collection.
    /// </returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// transformFactory.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull => source.TransformSafe(transformFactory, errorHandler, forceTransform);

    /// <summary>
    /// Transforms the object to a fully recursive tree, create a hierarchy based on the pivot function.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="pivotOn">The pivot on.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> SelectTree<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey> pivotOn)
        where TObject : class
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pivotOn.ThrowArgumentNullExceptionIfNull(nameof(pivotOn));

        return source.TransformToTree(pivotOn);
    }

    /// <summary>
    /// Filters the specified source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Filter(filter);
    }

    /// <summary>
    /// Creates a filtered stream which can be dynamically filtered.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        predicateChanged.ThrowArgumentNullExceptionIfNull(nameof(predicateChanged));

        return source.Filter(predicateChanged);
    }

    /// <summary>
    /// Creates a filtered stream which can be dynamically filtered.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Unit> reapplyFilter)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reapplyFilter.ThrowArgumentNullExceptionIfNull(nameof(reapplyFilter));

        return source.Filter(reapplyFilter);
    }

    /// <summary>
    /// Creates a filtered stream which can be dynamically filtered.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
    /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values.</param>
    /// <returns>An observable which emits the change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<Func<TObject, bool>> predicateChanged, IObservable<Unit> reapplyFilter)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        predicateChanged.ThrowArgumentNullExceptionIfNull(nameof(predicateChanged));
        reapplyFilter.ThrowArgumentNullExceptionIfNull(nameof(reapplyFilter));

        return source.Filter(predicateChanged, reapplyFilter);
    }
}
