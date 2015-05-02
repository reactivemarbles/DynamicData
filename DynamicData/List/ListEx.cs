using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// Extensions to help with maintainence of a list
	/// </summary>
	public static class ListEx
	{
		#region Binary Search / Lookup


		/// <summary>
		/// Performs a binary search on the specified collection.
		/// </summary>
		/// <typeparam name="TItem">The type of the item.</typeparam>
		/// <param name="list">The list to be searched.</param>
		/// <param name="value">The value to search for.</param>
		/// <returns></returns>
		public static int BinarySearch<TItem>(this IList<TItem> list, TItem value)
		{
			return BinarySearch(list, value, Comparer<TItem>.Default);
		}

		/// <summary>
		/// Performs a binary search on the specified collection.
		/// </summary>
		/// <typeparam name="TItem">The type of the item.</typeparam>
		/// <param name="list">The list to be searched.</param>
		/// <param name="value">The value to search for.</param>
		/// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
		/// <returns></returns>
		public static int BinarySearch<TItem>(this IList<TItem> list, TItem value, IComparer<TItem> comparer)
		{
			return list.BinarySearch(value, comparer.Compare);
		}

		/// <summary>
		/// Performs a binary search on the specified collection.
		/// 
		/// Thanks to http://stackoverflow.com/questions/967047/how-to-perform-a-binary-search-on-ilistt
		/// </summary>
		/// <typeparam name="TItem">The type of the item.</typeparam>
		/// <typeparam name="TSearch">The type of the searched item.</typeparam>
		/// <param name="list">The list to be searched.</param>
		/// <param name="value">The value to search for.</param>
		/// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
		/// <returns></returns>
		public static int BinarySearch<TItem, TSearch>(this IList<TItem> list, TSearch value, Func<TSearch, TItem, int> comparer)
		{
			if (list == null)
				throw new ArgumentNullException(nameof(list));

			if (comparer == null)
				throw new ArgumentNullException(nameof(comparer));
		

			int lower = 0;
			int upper = list.Count - 1;

			while (lower <= upper)
			{
				int middle = lower + (upper - lower) / 2;
				int comparisonResult = comparer(value, list[middle]);
				if (comparisonResult < 0)
				{
					upper = middle - 1;
				}
				else if (comparisonResult > 0)
				{
					lower = middle + 1;
				}
				else
				{
					return middle;
				}
			}

			return ~lower;
		}

		/// <summary>
		/// Lookups the item using the specified comparer. If matched, the item's index is also returned
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="item">The item.</param>
		/// <param name="equalityComparer">The equality comparer.</param>
		/// <returns></returns>
		public static Optional<ItemWithIndex<T>> Lookup<T>(this IEnumerable<T> source, T item, IEqualityComparer<T> equalityComparer = null)
		{
			var comparer = equalityComparer ?? EqualityComparer<T>.Default;

			var result = source.WithIndex().FirstOrDefault(x => comparer.Equals(x.Item, item));
			return !Equals(result, null) ? result : Optional.None<ItemWithIndex<T>>();
		}

		#endregion
		
		#region Amendment


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

			if (source is List<T>)
			{ }


			items.ForEach(source.Add);
		}

		public static void AddRange<T>(this IList<T> source, IEnumerable<T> items)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (items == null) throw new ArgumentNullException("items");

			if (source is List<T>)
			{
				((List<T>)source).AddRange(items);
			}
			else if (source is IExtendedList<T>)
			{
				((IExtendedList<T>)source).AddRange(items);
			}
			else
			{
				items.ForEach(source.Add);
			}

		}

		public static void AddRange<T>(this IList<T> source, IEnumerable<T> items,int index)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (items == null) throw new ArgumentNullException(nameof(items));

			if (source is List<T>)
			{
				((List<T>)source).InsertRange(index,items);
			}
			else if (source is IExtendedList<T>)
			{
				((IExtendedList<T>)source).InsertRange(items,index);
			}
			else
			{
				items.ForEach(source.Add);
			}

		}

		public static void AddOrInsertRange<T>(this IList<T> source, IEnumerable<T> items, int index)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (items == null) throw new ArgumentNullException("items");

			if (source is List<T>)
			{
				if (index >= 0)
				{
					((List<T>)source).AddRange(items, index);
				}
				else
				{
					((List<T>)source).AddRange(items);
				}

			}
			else if (source is ChangeAwareList<T>)
			{
				if (index >= 0)
				{
					((ChangeAwareList<T>)source).InsertRange(items, index);
				}
				else
				{
					((ChangeAwareList<T>)source).AddRange(items);
				}
			}
			else
			{
				items.ForEach(source.Add);
			}

		}


		public static void RemoveRange<T>(this IList<T> source,  int index,int count)
		{
			if (source == null) throw new ArgumentNullException("source");

			if (source is List<T>)
			{
				((List<T>)source).RemoveRange(index, count);
			}
			else if (source is ChangeAwareList<T>)
			{
				((ChangeAwareList<T>)source).RemoveRange(index, count);
			}
			else
			{
				throw new NotSupportedException("Cannot remove range from {0}".FormatWith(source.GetType()));
			}

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

			items.ForEach(t=>source.Remove(t));
		}

		/// <summary>
		/// Replaces the specified item.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="original">The original.</param>
		/// <param name="replacewith">The replacewith.</param>
		/// <exception cref="System.ArgumentNullException">source
		/// or
		/// items</exception>
		public static void Replace<T>(this IList<T> source, [NotNull]  T original, [NotNull] T replacewith)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (original == null) throw new ArgumentNullException("original");
			if (replacewith == null) throw new ArgumentNullException("replacewith");

			var index = source.IndexOf(original);
			source[index] = replacewith;
		}

		/// <summary>
		/// Ensures the collection has enough capacity where capacity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The enumerable.</param>
		/// <param name="changes">The changes.</param>
		/// <exception cref="ArgumentNullException">enumerable</exception>
		public static void EnsureCapacityFor<T>(this IEnumerable<T> source, IChangeSet changes)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (changes == null) throw new ArgumentNullException("changes");
			if (source is List<T>)
			{
				var list = (List<T>)source;
				list.Capacity = list.Count + changes.Adds;
			}
			else if (source is ISupportsCapcity)
			{
				var list = (ISupportsCapcity)source;
				list.Capacity = list.Count + changes.Adds;
			}
            else if (source is IChangeSet)
			{
				var original = (IChangeSet)source;
				original.Capacity = original.Count + changes.Count;
			}
		}


		#endregion

		#region Operators



		///// <summary>
		///// Clones the source list with the specified change set, transforming the items using the specified factory
		///// </summary>
		///// <typeparam name="TSource">The type of the source.</typeparam>
		///// <typeparam name="TDestination">The type of the destination.</typeparam>
		///// <param name="source">The source.</param>
		///// <param name="changes">The changes.</param>
		///// <param name="transformFactory">The transform factory.</param>
		///// <exception cref="System.ArgumentNullException">
		///// source
		///// or
		///// changes
		///// or
		///// transformFactory
		///// </exception>
		//public static void Transform<TSource,TDestination>(this IList<TDestination> source, IChangeSet<TSource> changes, Func<TSource,TDestination> transformFactory)
		//{
		//	if (source == null) throw new ArgumentNullException("source");
		//	if (changes == null) throw new ArgumentNullException("changes");
		//	if (transformFactory == null) throw new ArgumentNullException("transformFactory");

		//	source.EnsureCapacityFor(changes);
  //          changes.ForEach(change =>
		//	{
		//		switch (change.Reason)
		//		{
		//			case ChangeReason.Add:
		//				source.Insert(change.CurrentIndex, transformFactory(change.Current));
  //                      break;
		//			case ChangeReason.Update:
		//				if (change.CurrentIndex == change.PreviousIndex)
		//				{
		//					source[change.CurrentIndex] = transformFactory(change.Current);
		//				}
		//				else
		//				{
		//					source.RemoveAt(change.PreviousIndex);
		//					source.Insert(change.CurrentIndex, transformFactory(change.Current));
		//				}
		//				break;
		//			case ChangeReason.Remove:
		//				source.RemoveAt(change.CurrentIndex);
		//				break;
		//		}
		//	});

		//}


		///// <summary>
		///// Clones the source list with the specified change set
		///// </summary>
		///// <typeparam name="T"></typeparam>
		///// <param name="source">The source.</param>
		///// <param name="changes">The changes.</param>
		///// <exception cref="System.ArgumentNullException">
		///// source
		///// or
		///// changes
		///// </exception>
		//public static void Clone<T>(this IList<T> source, IChangeSet<T> changes)
		//{
		//	if (source == null) throw new ArgumentNullException("source");
		//	if (changes == null) throw new ArgumentNullException("changes");

		//	source.EnsureCapacityFor(changes);

  //          changes.ForEach(change =>
		//	{
		//		bool hasIndex = change.CurrentIndex >= 0;
		//		switch (change.Reason)
		//		{
		//			case ChangeReason.Add:
		//				if (hasIndex)
		//				{
		//					source.Insert(change.CurrentIndex, change.Current);
		//				}
		//				else
		//				{
		//					source.Add(change.Current);
		//				}
		//				break;
		//			case ChangeReason.Update:
		//				{
		//					if (hasIndex && change.CurrentIndex == change.PreviousIndex)
		//					{
		//						source[change.CurrentIndex] = change.Current;
		//					}
		//					else
		//					{
		//						//is this best? or replace + move?
		//						source.RemoveAt(change.PreviousIndex);
		//						source.Insert(change.CurrentIndex, change.Current);
		//					}
		//				}
		//				break;
		//			case ChangeReason.Remove:
		//				if (hasIndex)
		//				{
		//					source.RemoveAt(change.CurrentIndex);
		//				}
		//				else
		//				{
		//					source.Remove(change.Current);
		//				}
						
		//				break;
		//			case ChangeReason.Moved:
		//				if (!hasIndex)
		//					throw new UnspecifiedIndexException("Cannot move as an index was not specified");

		//				var collection = source as ChangeAwareCollection<T>;
		//				if (collection != null)
		//				{
		//					collection.Move(change.PreviousIndex, change.CurrentIndex);
  //                      }
		//				else
		//				{
		//					//check this works whether the index is 
		//					source.RemoveAt(change.PreviousIndex);
		//					source.Insert(change.CurrentIndex, change.Current);
		//				}

		//				break;
		//		}
		//	});


		//}

		///// <summary>
		///// Filters the source from the changes, using the specified predicate
		///// </summary>
		///// <typeparam name="T"></typeparam>
		///// <param name="source">The source.</param>
		///// <param name="changes">The changes.</param>
		///// <param name="predicate">The predicate.</param>
		//internal static void Filter<T>(this IList<T> source, IChangeSet<T> changes, Func<T, bool> predicate)
		//{
		//	if (source == null) throw new ArgumentNullException("source");
		//	if (changes == null) throw new ArgumentNullException("changes");
		//	if (predicate == null) throw new ArgumentNullException("predicate");

		//	changes.ForEach(change =>
		//	{

		//		switch (change.Reason)
		//		{
		//			case ChangeReason.Add:
		//				{
		//					var match = predicate(change.Current);
		//					if (match) source.Add(change.Current);
		//				}
		//				break;
		//			case ChangeReason.Update:
		//				{
		//					var match = predicate(change.Current);
		//					var wasMatch = predicate(change.Previous.Value);

		//					if (match)
		//					{
		//						if (wasMatch)
		//						{
		//							//an update, so get the latest index
		//							var previous = source.Lookup(change.Previous.Value, ReferenceEqualityComparer<T>.Instance)
		//												.ValueOrThrow(()=>new InvalidOperationException("Cannot find item. Expected to be in the list"));

		//							//replace inline
		//							source[previous.Index] = change.Current;
		//						}
		//						else
		//						{
		//							source.Add(change.Current);
		//						}
		//					}
		//					else
		//					{
		//						if (wasMatch)
		//							source.Remove(change.Previous.Value);
		//					}
		//				}

		//				break;
		//			case ListChangeReason.Remove:
		//				{
		//					var wasMatch = predicate(change.Current);
		//					if (wasMatch) source.Remove(change.Current);
		//				}
		//				break;
		//		}
		//	});


		//}
		#endregion
	}
}
