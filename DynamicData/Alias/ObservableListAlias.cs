using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using DynamicData.Annotations;
using DynamicData.Controllers;

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return source.Filter(predicate);
        }

        /// <summary>
        /// Filters source using the specified filter controller.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filterController">The filter controller.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// filterController</exception>
        public static IObservable<IChangeSet<T>> Where<T>(this IObservable<IChangeSet<T>> source, FilterController<T> filterController)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (filterController == null) throw new ArgumentNullException(nameof(filterController));
            return source.Filter(filterController);
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return source.Filter(predicate);
        }


        /// <summary>
        /// Filters source on the specified property using the specified predicate.
        /// 
        /// The filter will automatically reapply when a property changes 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector. When the property changes the filter specified will be re-evaluated</param>
        /// <param name="predicate">A predicate based on the object which contains the changed property</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject>> WhereProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source,
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (manyselector == null) throw new ArgumentNullException(nameof(manyselector));
            return source.TransformMany(manyselector);
        }

        #endregion
    }
}
