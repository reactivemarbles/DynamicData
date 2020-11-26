// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if P_LINQ
using System;

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    /// <summary>
    /// PLinq operators or Net4 and Net45 only.
    /// </summary>
    public static class ParallelOperators
    {
        /// <summary>
        /// Filters the stream using the specified predicate.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns>An observable which emits a change set.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new PFilter<TObject, TKey>(source, filter, parallelisationOptions).Run();
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function.</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns>An observable which emits a change set.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory.</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed.
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory, ParallelisationOptions parallelisationOptions)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (subscriptionFactory is null)
            {
                throw new ArgumentNullException(nameof(subscriptionFactory));
            }

            if (parallelisationOptions is null)
            {
                throw new ArgumentNullException(nameof(parallelisationOptions));
            }

            return new PSubscribeMany<TObject, TKey>(source, (t, v) => subscriptionFactory(t), parallelisationOptions).Run();
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function.</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns>An observable which emits a change set.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory.</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed.
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory, ParallelisationOptions parallelisationOptions)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (subscriptionFactory is null)
            {
                throw new ArgumentNullException(nameof(subscriptionFactory));
            }

            if (parallelisationOptions is null)
            {
                throw new ArgumentNullException(nameof(parallelisationOptions));
            }

            return new PSubscribeMany<TObject, TKey>(source, subscriptionFactory, parallelisationOptions).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="parallelisationOptions">The parallelisation options to be used on the transforms.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, ParallelisationOptions parallelisationOptions)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (parallelisationOptions is null)
            {
                throw new ArgumentNullException(nameof(parallelisationOptions));
            }

            return new PTransform<TDestination, TSource, TKey>(source, (t, p, k) => transformFactory(t, k), parallelisationOptions).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, ParallelisationOptions parallelisationOptions)
            where TKey : notnull
        {
            return new PTransform<TDestination, TSource, TKey>(source, (t, p, k) => transformFactory(t), parallelisationOptions).Run();
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
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, ParallelisationOptions parallelisationOptions)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory is null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            if (errorHandler is null)
            {
                throw new ArgumentNullException(nameof(errorHandler));
            }

            if (parallelisationOptions is null)
            {
                throw new ArgumentNullException(nameof(parallelisationOptions));
            }

            return new PTransform<TDestination, TSource, TKey>(source, (t, p, k) => transformFactory(t), parallelisationOptions, errorHandler).Run();
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
        /// <param name="parallelisationOptions">The parallelisation options to be used on the transforms.</param>
        /// <returns>
        /// A transformed update collection.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory.</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, ParallelisationOptions parallelisationOptions)
            where TKey : notnull
        {
            return new PTransform<TDestination, TSource, TKey>(source, (t, p, k) => transformFactory(t, k), parallelisationOptions, errorHandler).Run();
        }
    }
}

#endif