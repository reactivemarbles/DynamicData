using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

namespace DynamicData.Aggregation
{
    /// <summary>
	/// Aggregation extensions
	/// </summary>
	public static class AggregationEx
	{
		/// <summary>
		/// Fors the aggregation.
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		public static IObservable<IEnumerable<AggregateItem<TObject, TKey>>> ForAggregation<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
		{
			return source.Select(changeset => (IEnumerable<AggregateItem<TObject, TKey>>)new AggregateEnumerator<TObject, TKey>(changeset));
		}

        public static IObservable<int> Count<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.ForAggregation().Scan(0, (state, changes) =>
            {
                return changes.Aggregate(state, (current, aggregateItem) =>
                                aggregateItem.Type == AggregateType.Add
                                    ? state = state + 1
                                    : state = state - 1);
            });
        }


        public static IObservable<IReadOnlyCollection<TObject>> ForAggregate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.QueryWhenChanged(query => new ReadOnlyCollection<TObject>(query.Items,query.Count));
        }

        public static IObservable<IReadOnlyCollection<T>> ForAggregate<T>(this IObservable<IChangeSet<T>> source)
        {
            return source.QueryWhenChanged();
        }


        internal static IObservable<TResult> Accumlate<TObject, TKey, TResult>(this IObservable<IChangeSet<TObject, TKey>> source,
            TResult seed,
            Func<TObject, TResult> accessor,
            Func<TResult, TResult, TResult> addAction,
            Func<TResult, TResult, TResult> removeAction)
        {
            return source.ForAggregation().Scan(seed, (state, changes) =>
            {
                return changes.Aggregate(state, (current, aggregateItem) =>
                                aggregateItem.Type == AggregateType.Add
                                    ? addAction(current, accessor(aggregateItem.Item))
                                    : removeAction(current, accessor(aggregateItem.Item))
                );
            });
        }
	}
}