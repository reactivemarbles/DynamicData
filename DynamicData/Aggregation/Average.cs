using System;
using System.Collections.Generic;

using System.Linq;
using System.Reactive.Linq;

namespace DynamicData.Aggregation
{
    /// <summary>
	/// Average extensions
	/// </summary>
	public static class AverageEx
	{

        public static IObservable<double> Average<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, int> averageSelector, int fallbackValue = 0)
        {
            return source.ForAggregate().Select(item => item.Count == 0 ? fallbackValue : item.Average(averageSelector));
        }

        public static IObservable<double> Average<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> averageSelector, long fallbackValue = 0)
        {
            return source.ForAggregate().Select(item => item.Count == 0 ? fallbackValue : item.Average(averageSelector));
        }

        public static IObservable<double> Average<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> averageSelector, double fallbackValue = 0)
        {
            return source.ForAggregate().Select(item => item.Count == 0 ? fallbackValue : item.Average(averageSelector));
        }

        public static IObservable<decimal> Average<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> averageSelector, decimal fallbackValue = 0)
        {
            return source.ForAggregate().Select(item => item.Count == 0 ? fallbackValue : item.Average(averageSelector));
        }

        public static IObservable<float> Average<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> averageSelector, float fallbackValue = 0)
        {
            return source.ForAggregate().Select(item => item.Count == 0 ? fallbackValue : item.Average(averageSelector));
        }



        public static IObservable<double> Average<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, int> averageSelector, int fallbackValue = 0)
        {
            return source.Select(query => query.Count == 0 ? fallbackValue : query.Average(averageSelector));
        }

        public static IObservable<double> Average<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, long> averageSelector, long fallbackValue = 0)
        {
            return source.Select(query => query.Count == 0 ? fallbackValue : query.Average(averageSelector));
        }

        public static IObservable<double> Average<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, double> averageSelector, double fallbackValue = 0)
        {
            return source.Select(query => query.Count == 0 ? fallbackValue : query.Average(averageSelector));
        }

        public static IObservable<decimal> Average<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, decimal> averageSelector, decimal fallbackValue = 0)
        {
            return source.Select(query => query.Count == 0 ? fallbackValue : query.Average(averageSelector));

        }

        public static IObservable<float> Average<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, float> averageSelector, float fallbackValue = 0)
        {
            return source.Select(query => query.Count == 0 ? fallbackValue : query.Average(averageSelector));
        }

	}
}