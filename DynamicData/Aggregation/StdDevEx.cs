using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Aggregation
{
    /// <summary>
    /// Extensons for calculaing standard deviation
    /// </summary>
    public static class StdDevEx
    {
        #region From  IChangeSet<T>

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] Func<T, int> valueSelector, int fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        #endregion

        #region From  IChangeSet<TObject, TKey>

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] Func<TObject, int> valueSelector, int fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector, long fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector, double fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector, decimal fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector, float fallbackValue = 0)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        #endregion

        #region From  From IAggregateChangeSet<TObject>

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                    [NotNull] Func<T, int> valueSelector,
                                                    int fallbackValue = 0)
        {
            return source.StdDevCalc(t => (long)valueSelector(t),
                                     fallbackValue,
                                     (current, item) => new StdDev<long>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)),
                                     (current, item) => new StdDev<long>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)),
                                     values => Math.Sqrt(values.SumOfSquares - (values.SumOfItems * values.SumOfItems) / values.Count) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                    [NotNull] Func<T, long> valueSelector,
                                                    long fallbackValue = 0)
        {
            return source.StdDevCalc(valueSelector,
                                     fallbackValue,
                                     (current, item) => new StdDev<long>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)),
                                     (current, item) => new StdDev<long>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)),
                                     values => Math.Sqrt(values.SumOfSquares - (values.SumOfItems * values.SumOfItems) / values.Count) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                    [NotNull] Func<T, decimal> valueSelector,
                                                    decimal fallbackValue = 0M)
        {
            throw new NotImplementedException("For some reason there is a problem with decimal value inference");

            //return source.StdDevCalc(valueSelector,
            //    fallbackValue,
            //    (current, item) => new StdDev<decimal>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)),
            //    (current, item) => new StdDev<decimal>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)),
            //    values => Math.Sqrt((double)values.SumOfSquares - (double)(values.SumOfItems * values.SumOfItems) / values.Count) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                    [NotNull] Func<T, double> valueSelector,
                                                    double fallbackValue = 0)
        {
            return source.StdDevCalc(valueSelector,
                                     fallbackValue,
                                     (current, item) => new StdDev<double>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)),
                                     (current, item) => new StdDev<double>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)),
                                     values => Math.Sqrt(values.SumOfSquares - (values.SumOfItems * values.SumOfItems) / values.Count) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<double> StdDev<T>([NotNull] this IObservable<IAggregateChangeSet<T>> source,
                                                    [NotNull] Func<T, float> valueSelector,
                                                    float fallbackValue = 0)
        {
            return source.StdDevCalc(valueSelector,
                                     fallbackValue,
                                     (current, item) => new StdDev<float>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)),
                                     (current, item) => new StdDev<float>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)),
                                     values => Math.Sqrt(values.SumOfSquares - (values.SumOfItems * values.SumOfItems) / values.Count) * (1.0d / (values.Count - 1)));
        }

        private static IObservable<TResult> StdDevCalc<TObject, TValue, TResult>(this IObservable<IAggregateChangeSet<TObject>> source,
                                                                                 Func<TObject, TValue> valueSelector,
                                                                                 TResult fallbackValue,
                                                                                 [NotNull] Func<StdDev<TValue>, TValue, StdDev<TValue>> addAction,
                                                                                 [NotNull] Func<StdDev<TValue>, TValue, StdDev<TValue>> removeAction,
                                                                                 [NotNull] Func<StdDev<TValue>, TResult> resultAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            if (addAction == null) throw new ArgumentNullException(nameof(addAction));
            if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));
            if (resultAction == null) throw new ArgumentNullException(nameof(resultAction));

            return source.Scan(default(StdDev<TValue>), (state, changes) =>
            {
                return changes.Aggregate(state, (current, aggregateItem) =>
                                                    aggregateItem.Type == AggregateType.Add
                                                        ? addAction(current, valueSelector(aggregateItem.Item))
                                                        : removeAction(current, valueSelector(aggregateItem.Item))
                    );
            })
                         .Select(values => values.Count < 2 ? fallbackValue : resultAction(values));
        }

        #endregion
    }
}
