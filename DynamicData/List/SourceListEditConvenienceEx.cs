using System;
using System.Collections.Generic;
using DynamicData.Annotations;
using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Convenience methods for a source list
    /// </summary>
    public static class SourceListEditConvenienceEx
    {
        /// <summary>
        /// Loads the list with the specified items in an optimised manner i.e. calculates the differences between the old and new items
        ///  in the list and amends only the differences
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="alltems"></param>
        /// <param name="equalityComparer">The equality comparer used to determine whether an item has changed</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void EditDiff<T>([NotNull] this ISourceList<T> source,
            [NotNull] IEnumerable<T> alltems,
            IEqualityComparer<T> equalityComparer = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (alltems == null) throw new ArgumentNullException(nameof(alltems));
            var editDiff = new EditDiff<T>(source, equalityComparer);
            editDiff.Edit(alltems);
        }

        /// <summary>
        /// Clears all items from the specified source list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        public static void Clear<T>([NotNull] this ISourceList<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.Clear());
        }

        /// <summary>
        /// Adds the specified item to the source list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        public static void Add<T>([NotNull] this ISourceList<T> source, T item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.Add(item));
        }

        /// <summary>
        /// Adds the specified item to the source list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <param name="index">The index.</param>
        public static void Insert<T>([NotNull] this ISourceList<T> source, int index, T item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.Insert(index, item));
        }

        /// <summary>
        /// Adds the specified items to the source list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        public static void AddRange<T>([NotNull] this ISourceList<T> source, IEnumerable<T> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.AddRange(items));
        }

        /// <summary>
        /// Inserts the elements of a collection into the <see cref="T:System.Collections.Generic.List`1" /> at the specified index.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
        public static void InsertRange<T>([NotNull] this ISourceList<T> source, IEnumerable<T> items, int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.AddRange(items, index));
        }

        /// <summary>
        /// Removes the specified item from the source list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        public static void Remove<T>([NotNull] this ISourceList<T> source, T item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.Remove(item));
        }

        /// <summary>
        /// Removes the items from source in an optimised manner
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="itemsToRemove">The items to remove.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static void RemoveMany<T>([NotNull] this ISourceList<T> source, IEnumerable<T> itemsToRemove)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.RemoveMany(itemsToRemove));
        }

        /// <summary>
        /// Moves an item from the original to the destination index
        /// </summary>
        ///  <param name="source">The source.</param>
        /// <param name="original">The original.</param>
        /// <param name="destination">The destination.</param>
        public static void Move<T>([NotNull] this ISourceList<T> source, int original, int destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.Move(original, destination));
        }

        /// <summary>
        /// Removes a range of elements from the <see cref="T:System.Collections.Generic.List`1" />.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="index">The zero-based starting index of the range of elements to remove.</param>
        /// <param name="count">The number of elements to remove.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is less than 0.-or-<paramref name="count" /> is less than 0.</exception>
        /// <exception cref="T:System.ArgumentException"><paramref name="index" /> and <paramref name="count" /> do not denote a valid range of elements in the <see cref="T:System.Collections.Generic.List`1" />.</exception>
        public static void RemoveRange<T>([NotNull] this ISourceList<T> source, int index, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.RemoveRange(index, count));
        }

        /// <summary>
        /// Removes the element at the spedified index
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="index">The index.</param>
        public static void RemoveAt<T>([NotNull] this ISourceList<T> source, int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.RemoveAt(index));
        }

        /// <summary>
        /// Replaces the specified original with the destinaton object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="original">The original.</param>
        /// <param name="destination">The destination.</param>
        public static void Replace<T>([NotNull] this ISourceList<T> source, T original, T destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list.Replace(original, destination));
        }

        /// <summary>
        /// Replaces the item at the specified index with the new item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        public static void ReplaceAt<T>([NotNull] this ISourceList<T> source, int index, T item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(list => list[index] = item);
        }
    }
}
