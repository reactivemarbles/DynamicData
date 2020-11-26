// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Linq;

namespace DynamicData.Aggregation
{
    /// <summary>
    /// Extensions for calculating standard deviation.
    /// </summary>
    public static class StdDevEx
    {
        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0)
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> valueSelector, int fallbackValue)
            where TKey : notnull
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector, long fallbackValue)
            where TKey : notnull
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector, double fallbackValue)
            where TKey : notnull
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector, decimal fallbackValue)
            where TKey : notnull
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector, float fallbackValue = 0)
            where TKey : notnull
        {
            return source.ForAggregation().StdDev(valueSelector, fallbackValue);
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, int> valueSelector, int fallbackValue = 0)
        {
            return source.StdDevCalc(t => (long)valueSelector(t), fallbackValue, (current, item) => new StdDev<long>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<long>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, long> valueSelector, long fallbackValue = 0)
        {
            return source.StdDevCalc(valueSelector, fallbackValue, (current, item) => new StdDev<long>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<long>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, decimal> valueSelector, decimal fallbackValue = 0M)
        {
            throw new NotImplementedException("For some reason there is a problem with decimal value inference");

            //// return source.StdDevCalc(valueSelector,
            ////    fallbackValue,
            ////    (current, item) => new StdDev<decimal>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)),
            ////    (current, item) => new StdDev<decimal>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)),
            ////    values => Math.Sqrt((double)values.SumOfSquares - (double)(values.SumOfItems * values.SumOfItems) / values.Count) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, double> valueSelector, double fallbackValue = 0)
        {
            return source.StdDevCalc(valueSelector, fallbackValue, (current, item) => new StdDev<double>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<double>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));
        }

        /// <summary>
        /// Continual computation of the standard deviation of the  values in the underlying data source.
        /// </summary>
        /// <typeparam name="T">The type of the item.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <param name="fallbackValue">The fallback value.</param>
        /// <returns>An observable which emits the standard deviation value.</returns>
        public static IObservable<double> StdDev<T>(this IObservable<IAggregateChangeSet<T>> source, Func<T, float> valueSelector, float fallbackValue = 0)
        {
            return source.StdDevCalc(valueSelector, fallbackValue, (current, item) => new StdDev<float>(current.Count + 1, current.SumOfItems + item, current.SumOfSquares + (item * item)), (current, item) => new StdDev<float>(current.Count - 1, current.SumOfItems - item, current.SumOfSquares - (item * item)), values => Math.Sqrt(values.SumOfSquares - ((values.SumOfItems * values.SumOfItems) / values.Count)) * (1.0d / (values.Count - 1)));
        }

        private static IObservable<TResult> StdDevCalc<TObject, TValue, TResult>(this IObservable<IAggregateChangeSet<TObject>> source, Func<TObject, TValue> valueSelector, TResult fallbackValue, Func<StdDev<TValue>, TValue, StdDev<TValue>> addAction, Func<StdDev<TValue>, TValue, StdDev<TValue>> removeAction, Func<StdDev<TValue>, TResult> resultAction)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (valueSelector is null)
            {
                throw new ArgumentNullException(nameof(valueSelector));
            }

            if (addAction is null)
            {
                throw new ArgumentNullException(nameof(addAction));
            }

            if (removeAction is null)
            {
                throw new ArgumentNullException(nameof(removeAction));
            }

            if (resultAction is null)
            {
                throw new ArgumentNullException(nameof(resultAction));
            }

            return source.Scan(default(StdDev<TValue>), (state, changes) => { return changes.Aggregate(state, (current, aggregateItem) => aggregateItem.Type == AggregateType.Add ? addAction(current, valueSelector(aggregateItem.Item)) : removeAction(current, valueSelector(aggregateItem.Item))); }).Select(values => values.Count < 2 ? fallbackValue : resultAction(values));
        }
    }
}