// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using DynamicData.Annotations;

namespace DynamicData.Alias
{
    /// <summary>
    /// Observable cache alias names
    /// </summary>
    public static class ObservableListAlias
    {
        #region Filter -> Where

        /// <summary>
        /// Filters the source using the specified valueSelector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicate">The valueSelector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> Where<T>(this IObservable<IChangeSet<T>> source, Func<T, bool> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return source.Filter(predicate);
        }

        /// <summary>
        /// Filters source using the specified filter observable predicate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// filterController</exception>
        public static IObservable<IChangeSet<T>> Where<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] IObservable<Func<T, bool>> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return source.Filter(predicate);
        }

        #endregion 

        #region Transform -> Select

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// valueSelector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Select<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> transformFactory)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (transformFactory == null)
            {
                throw new ArgumentNullException(nameof(transformFactory));
            }

            return source.Transform(transformFactory);
        }

        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child.
        /// **** Assumes each child can only have one  parent - support for children with multiple parents is a work in progresss
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// manyselector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> SelectMany<TDestination, TSource>([NotNull] this IObservable<IChangeSet<TSource>> source, [NotNull] Func<TSource, IEnumerable<TDestination>> manyselector)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (manyselector == null)
            {
                throw new ArgumentNullException(nameof(manyselector));
            }

            return source.TransformMany(manyselector);
        }

        #endregion
    }
}
