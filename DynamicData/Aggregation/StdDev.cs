using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Aggregation
{

    public static class StdDevEx
    {
        public static IObservable<double> StdDev<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,[NotNull] Func<TObject, int> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.ForAggregate().Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.ForAggregate().Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector)
        {
            return source.ForAggregate().Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector)
        {
            return source.ForAggregate().Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, int> valueSelector)
        {
            return source.Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, long> valueSelector)
        {
            return source.Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, double> valueSelector)
        {
            return source.Select(items => items.Compute(valueSelector));
        }

        public static IObservable<double> StdDev<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, decimal> valueSelector)
        {
         return source.Select(items => items.Compute(valueSelector));

        }

        public static IObservable<double> StdDev<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, float> valueSelector)
        {
            return source.Select(items => items.Compute(valueSelector));
        }

        /// <summary>
        /// These stddev calculations have unashamedly lifted from
        ///  https://clinq.codeplex.com/
        /// A big thanks
        /// </summary>

        #region Calculations

        public static double Compute<T>(this IReadOnlyCollection<T> dataList, Func<T, int> columnSelector)
        {
            double finalValue = 0;
            double variance = 0.0;

            int count = dataList.Count;
            double average = dataList.Average(columnSelector);

            if (count == 0) return finalValue;

            foreach (T item in dataList)
            {
                int columnValue = columnSelector(item);
                variance += Math.Pow(columnValue - average, 2);
            }

            finalValue = Math.Sqrt(variance / count);
            return finalValue;
        }

        public static double Compute<T>(this IReadOnlyCollection<T> dataList, Func<T, double> columnSelector)
        {
            double finalValue = 0;
            double variance = 0.0;

            int count = dataList.Count;
            double average = dataList.Average(columnSelector);

            if (count == 0) return finalValue;

            foreach (T item in dataList)
            {
                double columnValue = columnSelector(item);
                variance += Math.Pow(columnValue - average, 2);
            }

            finalValue = Math.Sqrt(variance / count);
            return finalValue;
        }

        public static double Compute<T>(this IReadOnlyCollection<T> dataList, Func<T, float> columnSelector)
        {
            double finalValue = 0;
            double variance = 0.0;

            int count = dataList.Count;
            double average = dataList.Average(columnSelector);

            if (count == 0) return finalValue;

            foreach (T item in dataList)
            {
                float columnValue = columnSelector(item);
                variance += Math.Pow(columnValue - average, 2);
            }

            finalValue = Math.Sqrt(variance / count);
            return finalValue;
        }

        public static double Compute<T>(this IReadOnlyCollection<T> dataList, Func<T, long> columnSelector)
        {
            double finalValue = 0;
            double variance = 0.0;

            int count = dataList.Count;
            double average = dataList.Average(columnSelector);

            if (count == 0) return finalValue;

            foreach (T item in dataList)
            {
                long columnValue = columnSelector(item);
                variance += Math.Pow(columnValue - average, 2);
            }

            finalValue = Math.Sqrt(variance / count);
            return finalValue;
        }

        public static double Compute<T>(this IReadOnlyCollection<T> dataList, Func<T, decimal> columnSelector)
        {
            double finalValue = 0;
            double variance = 0.0;

            int count = dataList.Count;
            double average = (double)dataList.Average(columnSelector);

            if (count == 0) return finalValue;

            foreach (T item in dataList)
            {
                double columnValue = (double)columnSelector(item);
                variance += Math.Pow(columnValue - average, 2);
            }

            finalValue = Math.Sqrt(variance / count);
            return finalValue;
        }

        #endregion

    }

}
