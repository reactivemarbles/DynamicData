using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// Extensions to help with maintainence of a list
	/// </summary>
	public static class ListEx
	{

		/// <summary>
		/// Lookups the item using the specified comparer. If matched, the item's index is also returned
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="item">The item.</param>
		/// <param name="equalityComparer">The equality comparer.</param>
		/// <returns></returns>
		public static Optional<ItemWithIndex<T>> Lookup<T>(this IEnumerable<T> source, T item, IEqualityComparer<T> equalityComparer)
		{
			var comparer = equalityComparer ?? EqualityComparer<T>.Default;

			var result = source.WithIndex().FirstOrDefault(x => comparer.Equals(x.Item, item));
			return !Equals(result, null) ? result : Optional.None<ItemWithIndex<T>>();
		}

		/// <summary>
		/// Adds the  items to the specified list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="items">The items.</param>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// items
		/// </exception>
		public static void Add<T>(this IList<T> source, IEnumerable<T> items)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (items == null) throw new ArgumentNullException("items");

			items.ForEach(source.Add);
		}

		/// <summary>
		/// Removes the  items from the specified list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="items">The items.</param>
		/// <exception cref="System.ArgumentNullException">
		/// source
		/// or
		/// items
		/// </exception>
		public static void Remove<T>(this IList<T> source, IEnumerable<T> items)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (items == null) throw new ArgumentNullException("items");

			items.ForEach(source.Add);
		}



		internal static void Clone<T>(this IList<T> source, IChangeSet<T> changes)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (changes == null) throw new ArgumentNullException("changes");

			changes.ForEach(change =>
			{

				switch (change.Reason)
				{
					case ChangeReason.Add:
						source.Insert(change.CurrentIndex, change.Current);
						break;
					case ChangeReason.Update:
						{
							if (change.CurrentIndex == change.PreviousIndex)
							{
								source[change.CurrentIndex] = change.Current;
							}
							else
							{
								//check this works whether the index is 
								source.RemoveAt(change.PreviousIndex);
								source.Insert(change.CurrentIndex, change.Current);
							}
						}

						break;
					case ChangeReason.Remove:
						source.RemoveAt(change.CurrentIndex);
						break;
					case ChangeReason.Moved:
						//check this works whether the index is 
						source.RemoveAt(change.PreviousIndex);
						source.Insert(change.CurrentIndex, change.Current);

						break;
					default:
						break;
				}
			});


		}

	}
}
