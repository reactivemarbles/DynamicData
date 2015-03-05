using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
	/// <summary>
	/// A readonly observable list, providing  observable methods
	/// as well as data access methods
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface IObservableList<T> : IDisposable
	{
		/// <summary>
		/// Connect to the observable list and observe and changes
		/// starting with the initial items in the cache 
		/// </summary>
		/// <returns></returns>
		IObservable<IChangeSet<T>> Connect(Func<T, bool> predicate=null);
		
		/// <summary>
		/// Observe the count changes, starting with the inital items count
		/// </summary>
		IObservable<int> CountChanged { get; }

		/// <summary>
		/// Lookups the item using the specified equality comparer
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="equalityComparer">The equality comparer.</param>
		/// <returns>An ItemWithIndex container which contains the item with it's index</returns>
		Optional<ItemWithIndex<T>> Lookup(T item, IEqualityComparer<T> equalityComparer = null);
		
		/// <summary>
		/// Items enumerable
		/// </summary>
		IEnumerable<T> Items { get; }

		/// <summary>
		/// Gets the count.
		/// </summary>
		int Count { get; }
	}
}