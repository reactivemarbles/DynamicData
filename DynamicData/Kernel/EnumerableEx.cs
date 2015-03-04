using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.List;

namespace DynamicData.Kernel
{
	public static class EnumerableEx
	{

		/// <summary>
		/// Returns an object with it's current index.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		internal static IEnumerable<ItemWithIndex<T>> WithIndex<T>(this IEnumerable<T> source)
		{

			return source.Select((item, index) => new ItemWithIndex<T>(item, index));
		}




		internal static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
		{
			foreach (var item in source)
			{
				action(item);
			}
		}



		internal static void ForEach<TObject>(this IEnumerable<TObject> source, Action<TObject, int> action)
		{
			int i = 0;
			foreach (var item in source)
			{
				action(item, i);
				i++;
			}
		}

		internal static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> source)
		{
			return source ?? Enumerable.Empty<T>();
		}

		internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
		{
			return new HashSet<T>(source);
		}

	}
}