using System.Collections.Generic;

namespace DynamicData
{
	/// <summary>
	/// 
	/// </summary>
	public static class SourceListEditConvenienceEx
	{

		/// <summary>
		/// Clears all items from the specified source list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		public static void Clear<T>(this ISourceList<T> source)
		{
			source.Edit(list => list.Clear());
		}

		/// <summary>
		/// Adds the specified item to the source list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="item">The item.</param>
		public static void Add<T>(this ISourceList<T> source, T item)
		{
			source.Edit(list=>list.Add(item));
		}

		/// <summary>
		/// Adds the specified items to the source list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="items">The items.</param>
		public static void AddRange<T>(this ISourceList<T> source, IEnumerable<T> items)
		{
			source.Edit(list => list.AddRange(items));
		}

		/// <summary>
		/// Removes the specified item from the source list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="item">The item.</param>
		public static void Remove<T>(this ISourceList<T> source, T item)
		{
			source.Edit(list => list.Remove(item));
		}

		/// <summary>
		/// Removes the specified items from the source list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="items">The items.</param>
		public static void RemoveRange<T>(this ISourceList<T> source, IEnumerable<T> items)
		{
			source.Edit(list => list.Remove(items));
		}


		/// <summary>
		/// Removes the element at the spedified index
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="index">The index.</param>
		public static void RemoveAt<T>(this ISourceList<T> source, int index)
		{
			source.Edit(list => list.RemoveAt(index));
		}


		/// <summary>
		/// Replaces the specified original with the destinaton object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="original">The original.</param>
		/// <param name="destination">The destination.</param>
		public static void Replace<T>(this ISourceList<T> source, T original, T destination)
		{
			source.Edit(list => list.Replace(original, destination));
		}

		/// <summary>
		/// Replaces the item at 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="index">The index.</param>
		/// <param name="item">The item.</param>
		public static void Replace<T>(this ISourceList<T> source, int index, T item)
		{
			source.Edit(list => list[index]=item);
		}

		//public static int ElementAt<T>(this ISourceList<T> source, int index)
		//{
		//	source.Edit(list => list[index] = item);
		//}
	}
}