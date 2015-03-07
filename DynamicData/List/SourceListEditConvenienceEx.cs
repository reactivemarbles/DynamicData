using System.Collections.Generic;

namespace DynamicData
{
	/// <summary>
	/// 
	/// </summary>
	public static class SourceListEditConvenienceEx
	{

		public static void Clear<T>(this ISourceList<T> source)
		{
			source.Edit(list => list.Clear());
		}

		public static void Add<T>(this ISourceList<T> source, T item)
		{
			source.Edit(list=>list.Add(item));
		}

		public static void AddRange<T>(this ISourceList<T> source, IEnumerable<T> items)
		{
			source.Edit(list => list.Add(items));
		}

		public static void Remove<T>(this ISourceList<T> source, T item)
		{
			source.Edit(list => list.Remove(item));
		}

		public static void RemoveAt<T>(this ISourceList<T> source, int index)
		{
			source.Edit(list => list.RemoveAt(index));
		}

		public static void RemoveRange<T>(this ISourceList<T> source, IEnumerable<T> items)
		{
			source.Edit(list => list.Remove(items));
		}

		public static void Replace<T>(this ISourceList<T> source, T original, T destination)
		{
			source.Edit(list => list.Replace(original, destination));
		}

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