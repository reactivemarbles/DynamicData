using System;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Aggregation
{
    /// <summary>
    /// Aggregation extensions
    /// </summary>
    public static class SumEx
    {
        #region From IChangeSet<TObject>

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<int> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                          [NotNull] Func<TObject, int> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<int> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, int?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<long> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                           [NotNull] Func<TObject, long> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<long> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, long?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<double> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                             [NotNull] Func<TObject, double> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<double> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, double?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<decimal> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                              [NotNull] Func<TObject, decimal> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<decimal> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, decimal?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<float> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                            [NotNull] Func<TObject, float> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<float> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, float?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        #endregion

        #region From IChangeSet<TObject>

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<int> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                              [NotNull] Func<T, int> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<int> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, int?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<long> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                               [NotNull] Func<T, long> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<long> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, long?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<double> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                                 [NotNull] Func<T, double> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<double> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, double?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<decimal> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                                  [NotNull] Func<T, decimal> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<decimal> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, decimal?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<float> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                                [NotNull] Func<T, float> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<float> Sum<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, float?> valueSelector)
        {
            return source.ForAggregation().Sum(valueSelector);
        }

        #endregion

        #region From IAggregateChangeSet<TObject>

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<int> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                              [NotNull] Func<T, int> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0,
                                    valueSelector,
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<int> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, int?> valueSelector)
        {
            return source.Accumlate(0,
                                    t => valueSelector(t).GetValueOrDefault(),
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<long> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                               [NotNull] Func<T, long> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0,
                                    valueSelector,
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<long> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, long?> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0L,
                                    t => valueSelector(t).ValueOr(0),
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<double> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                 [NotNull] Func<T, double> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0,
                                    valueSelector,
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<double> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, double?> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0D,
                                    t => valueSelector(t).ValueOr(0),
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<decimal> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                  [NotNull] Func<T, decimal> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0,
                                    valueSelector,
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<decimal> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, decimal?> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0M,
                                    t => valueSelector(t).ValueOr(0),
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<float> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                [NotNull] Func<T, float> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0,
                                    valueSelector,
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        /// <summary>
        /// Continual computes the sum of values matching the value selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        public static IObservable<float> Sum<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, float?> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.Accumlate(0F,
                                    t => valueSelector(t).ValueOr(0),
                                    (current, value) => current + value,
                                    (current, value) => current - value);
        }

        #endregion
    }
}
