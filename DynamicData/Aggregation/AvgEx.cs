using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Aggregation
{
    /// <Avgmary>
    /// Average extensions
    /// </Avgmary>
    public static class AvgEx
    {
        #region From IChangeSet<TObject, TKey>

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, int> valueSelector, int emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, int?> valueSelector, int emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                             [NotNull] Func<TObject, long> valueSelector, long emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, long?> valueSelector, long emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                             [NotNull] Func<TObject, double> valueSelector, double emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, double?> valueSelector, double emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<decimal> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                              [NotNull] Func<TObject, decimal> valueSelector, decimal emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<decimal> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, decimal?> valueSelector, decimal emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<float> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                            [NotNull] Func<TObject, float> valueSelector, float emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<float> Avg<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, float?> valueSelector, float emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        #endregion

        #region From IChangeSet<TObject>

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                                 [NotNull] Func<T, int> valueSelector, int emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, int?> valueSelector, int emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, long> valueSelector, long emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, long?> valueSelector, long emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                                 [NotNull] Func<T, double> valueSelector, double emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, double?> valueSelector, double emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<decimal> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                                  [NotNull] Func<T, decimal> valueSelector, decimal emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<decimal> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, decimal?> valueSelector, decimal emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<float> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source,
                                                [NotNull] Func<T, float> valueSelector, float emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<float> Avg<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, float?> valueSelector, float emptyValue = 0)
        {
            return source.ForAggregation().Avg(valueSelector, emptyValue);
        }

        #endregion

        #region From IAggregateChangeSet<TObject>

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                 [NotNull] Func<T, int> valueSelector, int emptyValue = 0)
        {
            return source.AvgCalc(valueSelector,
                                  emptyValue,
                                  (current, item) => new Avg<int>(current.Count + 1, current.Sum + item),
                                  (current, item) => new Avg<int>(current.Count - 1, current.Sum - item),
                                  values => values.Sum / (double)values.Count);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, int?> valueSelector, int emptyValue = 0)
        {
            return source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);
        }

        /// <summary>
        /// Averages the specified value selector.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="emptyValue">The empty value.</param>
        /// <returns></returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, long> valueSelector, long emptyValue = 0)
        {
            return source.AvgCalc(valueSelector,
                                  emptyValue,
                                  (current, item) => new Avg<long>(current.Count + 1, current.Sum + item),
                                  (current, item) => new Avg<long>(current.Count - 1, current.Sum - item),
                                  values => values.Sum / (double)values.Count);
            ;
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, long?> valueSelector, long emptyValue = 0)
        {
            return source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                 [NotNull] Func<T, double> valueSelector, double emptyValue = 0)
        {
            return source.AvgCalc(valueSelector,
                                  emptyValue,
                                  (current, item) => new Avg<double>(current.Count + 1, current.Sum + item),
                                  (current, item) => new Avg<double>(current.Count - 1, current.Sum - item),
                                  values => values.Sum / (double)values.Count);
            ;
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<double> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, double?> valueSelector, double emptyValue = 0)
        {
            return source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<decimal> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                  [NotNull] Func<T, decimal> valueSelector, decimal emptyValue = 0)
        {
            return source.AvgCalc(valueSelector,
                                  emptyValue,
                                  (current, item) => new Avg<decimal>(current.Count + 1, current.Sum + item),
                                  (current, item) => new Avg<decimal>(current.Count - 1, current.Sum - item),
                                  values => values.Sum / values.Count);
        }

        /// <summary>
        /// Averages the specified value selector.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="emptyValue">The empty value.</param>
        /// <returns></returns>
        public static IObservable<decimal> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, decimal?> valueSelector, decimal emptyValue = 0)
        {
            return source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<float> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                [NotNull] Func<T, float> valueSelector, float emptyValue = 0)
        {
            return source.AvgCalc(valueSelector,
                                  emptyValue,
                                  (current, item) => new Avg<float>(current.Count + 1, current.Sum + item),
                                  (current, item) => new Avg<float>(current.Count - 1, current.Sum - item),
                                  values => values.Sum / values.Count);
            ;
        }

        /// <summary>
        /// Continuous calculation of the average of the underlying data source.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source observable</param>
        /// <param name="valueSelector">The function which returns the value</param>
        /// <param name="emptyValue">The resulting average value when there is no data</param>
        /// <returns>
        /// An obervable of averages
        /// </returns>
        public static IObservable<float> Avg<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source, [NotNull] Func<T, float?> valueSelector, float emptyValue = 0)
        {
            return source.Avg(t => valueSelector(t).GetValueOrDefault(), emptyValue);
        }

        #endregion

        private static IObservable<TResult> AvgCalc<TObject, TValue, TResult>(this IObservable<IAggregateChangeSet<TObject>> source,
                                                                              Func<TObject, TValue> valueSelector,
                                                                              TResult fallbackValue,
                                                                              [NotNull] Func<Avg<TValue>, TValue, Avg<TValue>> addAction,
                                                                              [NotNull] Func<Avg<TValue>, TValue, Avg<TValue>> removeAction,
                                                                              [NotNull] Func<Avg<TValue>, TResult> resultAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            if (addAction == null) throw new ArgumentNullException(nameof(addAction));
            if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));
            if (resultAction == null) throw new ArgumentNullException(nameof(resultAction));

            return source.Scan(default(Avg<TValue>), (state, changes) =>
            {
                return changes.Aggregate(state, (current, aggregateItem) =>
                                                    aggregateItem.Type == AggregateType.Add
                                                        ? addAction(current, valueSelector(aggregateItem.Item))
                                                        : removeAction(current, valueSelector(aggregateItem.Item))
                    );
            })
                         .Select(values => values.Count == 0 ? fallbackValue : resultAction(values));
        }
    }
}
