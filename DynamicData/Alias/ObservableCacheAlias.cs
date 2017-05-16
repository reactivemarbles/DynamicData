using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Alias
{
    /// <summary>
    /// Observable cache alias names
    /// </summary>
    public static class ObservableCacheAlias
    {
        #region  Filter -> Where

        /// <summary>
        /// Filters the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filter">The filter.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Filter(filter);
        }

        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] IObservable<Func<TObject, bool>> predicateChanged)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicateChanged == null) throw new ArgumentNullException(nameof(predicateChanged));
            return source.Filter(predicateChanged);
        }

        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] IObservable<Unit> reapplyFilter)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (reapplyFilter == null) throw new ArgumentNullException(nameof(reapplyFilter));
            return source.Filter(reapplyFilter);
        }


        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values</param>
        /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Where<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] IObservable<Func<TObject, bool>> predicateChanged,
            [NotNull] IObservable<Unit> reapplyFilter)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicateChanged == null) throw new ArgumentNullException(nameof(predicateChanged));
            if (reapplyFilter == null) throw new ArgumentNullException(nameof(reapplyFilter));
            return source.Filter(predicateChanged, reapplyFilter);
        }



        /// <summary>
        /// Filters source on the specified property using the specified predicate.
        /// 
        /// The filter will automatically reapply when a property changes 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector. When the property changes a the filter specified will be re-evaluated</param>
        /// <param name="predicate">A predicate based on the object which contains the changed property</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> WhereProperty<TObject, TKey, TProperty>(this IObservable<IChangeSet<TObject, TKey>> source,
                Expression<Func<TObject, TProperty>> propertySelector,
                Func<TObject, bool> predicate) where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return source.FilterOnProperty(propertySelector, predicate);
        }

        #endregion

        #region Transform -> Select


        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>#
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, TKey, TDestination> transformFactory,
                                                                                                         IObservable<Unit> forceTransform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (forceTransform == null) throw new ArgumentNullException(nameof(forceTransform));
            return source.Transform(transformFactory, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, TKey, TDestination> transformFactory,
                                                                                                         IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            return source.Transform(transformFactory, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TDestination> transformFactory,
            IObservable<Unit> forceTransform)
        {
            return source.Transform(transformFactory, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Select<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, TDestination> transformFactory,
                                                                                                         IObservable<Func<TSource, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return source.Transform(transformFactory, forceTransform);
        }

        /// <summary>
        /// Transforms the object to a fully recursive tree, create a hiearchy based on the pivot function
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pivotOn">The pivot on.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> SelectTree<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                        [NotNull] Func<TObject, TKey> pivotOn)
            where TObject : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pivotOn == null) throw new ArgumentNullException(nameof(pivotOn));
            return source.TransformToTree(pivotOn);
        }

        #endregion

        #region Transform many -> SelectMany


        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child. 
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="keySelector">The key selector which must be unique across all</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> SelectMany<TDestination, TDestinationKey, TSource, TSourceKey>(
            this IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, IEnumerable<TDestination>> manyselector, Func<TDestination, TDestinationKey> keySelector)
        {
            return source.TransformMany(manyselector, keySelector);
        }



        #endregion

        #region Transform safe -> SelectSafe

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
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                             Func<TSource, TDestination> transformFactory,
                                                                                                             Action<Error<TSource, TKey>> errorHandler,
                                                                                                             IObservable<Unit> forceTransform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));
            if (forceTransform == null) throw new ArgumentNullException(nameof(forceTransform));

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
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                             Func<TSource, TDestination> transformFactory,
                                                                                                             Action<Error<TSource, TKey>> errorHandler,
                                                                                                             IObservable<Func<TSource, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

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
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TKey, TDestination> transformFactory,
            Action<Error<TSource, TKey>> errorHandler,
            IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

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
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> SelectSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                             Func<TSource, TKey, TDestination> transformFactory,
                                                                                                             Action<Error<TSource, TKey>> errorHandler,
                                                                                                             IObservable<Unit> forceTransform)
        {
            return source.TransformSafe(transformFactory, errorHandler, forceTransform);
        }

        #endregion
    }
}
