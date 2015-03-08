using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace DynamicData
{
	internal class AggregateEnumerator<TObject, TKey> : IEnumerable<AggregateItem<TObject, TKey>>
	{
		private readonly IChangeSet<TObject, TKey> _source;

		public AggregateEnumerator(IChangeSet<TObject, TKey> source)
		{
			_source = source;
		}

		public IEnumerator<AggregateItem<TObject, TKey>> GetEnumerator()
		{
			foreach (var change in _source)
			{
				switch (change.Reason)
				{
					case ChangeReason.Add:
						yield return new AggregateItem<TObject, TKey>(AggregateType.Add, change.Current, change.Key);
						break;
					case ChangeReason.Update:
						yield return new AggregateItem<TObject, TKey>(AggregateType.Remove, change.Previous.Value, change.Key);
						yield return new AggregateItem<TObject, TKey>(AggregateType.Add, change.Current, change.Key);
						break;
					case ChangeReason.Remove:
						yield return new AggregateItem<TObject, TKey>(AggregateType.Remove, change.Current, change.Key);
						break;
					default:
						continue;
				}
			}
			;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

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

		/// <summary>
		/// Fors the scan.
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TAccumulate">The type of the accumulate.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="seed">The seed.</param>
		/// <param name="accumulator">The accumulator.</param>
		/// <returns></returns>
		public static IObservable<TAccumulate> ForScan<TObject, TKey, TAccumulate>(this IObservable<IChangeSet<TObject, TKey>> source,
			TAccumulate seed,
			Func<IEnumerable<AggregateItem<TObject, TKey>>, TAccumulate> accumulator)
		{
			return source.ForAggregation().Scan(seed, (state, result) => accumulator(result));
		}
	}
}