using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Aggregation
{
    /// <summary>
	/// Aggregation extensions
	/// </summary>
	public static class AggregationSumEx
	{

        #region From cache

        public static IObservable<int> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject,int> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        public static IObservable<long> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, long> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        public static IObservable<decimal> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, decimal> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);

        }

        public static IObservable<float> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, float> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        public static IObservable<double> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, double> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        #endregion

        #region From list

        public static IObservable<int> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject>> source, [NotNull] Func<TObject, int> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        public static IObservable<long> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject>> source, Func<TObject, long> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        public static IObservable<double> Sum<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject>> source, Func<TObject, double> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        public static IObservable<decimal> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, decimal> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        public static IObservable<float> Sum<TObject, TKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, float> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return source.ForAggregate().Sum(valueSelector);
        }

        #endregion

        #region From readonly list

        public static IObservable<int> Sum<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, int> valueSelector)
        {
            return source.Select(items => items.Sum(valueSelector));
        }

        public static IObservable<long> Sum<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, long> valueSelector)
        {
            return source.Select(items => items.Sum(valueSelector));
        }

        public static IObservable<double> Sum<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, double> valueSelector)
        {
            return source.Select(items => items.Sum(valueSelector));
        }

        public static IObservable<decimal> Sum<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, decimal> valueSelector)
        {
            return source.Select(items => items.Sum(valueSelector));

        }

        public static IObservable<float> Sum<TObject>(this IObservable<IReadOnlyCollection<TObject>> source, Func<TObject, float> valueSelector)
        {
            return source.Select(items => items.Sum(valueSelector));
        }


        #endregion

    }
}