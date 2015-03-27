using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData
{
	/// <summary>
	/// Multipe change container
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class RangeChange<T> : IEnumerable<T>
	{
		private readonly List<T> _items;
		/// <summary>
		/// Gets the index initial index i.e. for the initial starting point of the range insertion
		/// </summary>
		/// <value>
		/// The index.
		/// </value>
		public int Index { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="RangeChange{T}"/> class.
		/// </summary>
		/// <param name="items">The items.</param>
		/// <param name="index">The index.</param>
		public RangeChange(IEnumerable<T> items, int index = -1)
		{
			Index = index;
			_items = items as List<T> ?? items.ToList();
		}

		/// <summary>
		///     The total update count
		/// </summary>
		public int Count => _items.Count;


		/// <summary>
		/// Gets the enumerator.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<T> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String" /> that represents this instance.
		/// </returns>
		public override string ToString()
		{
			return string.Format("Range<{0}>. Count={1}", typeof(T).Name, Count);
		}

	}

}