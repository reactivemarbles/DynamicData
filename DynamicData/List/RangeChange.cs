using System.Collections;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Multipe change container
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RangeChange<T> : IEnumerable<T>
    {
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
        /// Adds the specified item to the range.
        /// </summary>
        /// <param name="item">The item.</param>
        public void Add(T item)
        {
            _items.Add(item);
        }

        /// <summary>
        /// Inserts the  item in the range at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        public void Insert(int index, T item)
        {
            _items.Insert(index, item);
        }

        /// <summary>
        /// Sets the index of the starting index of the range
        /// </summary>
        /// <param name="index">The index.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void SetStartingIndex(int index)
        {
            Index = index;
        }

        private readonly List<T> _items;

        /// <summary>
        ///     The total update count
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Gets the index initial index i.e. for the initial starting point of the range insertion
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        public int Index { get; private set; }

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
            return $"Range<{typeof(T).Name}>. Count={Count}";
        }
    }
}
