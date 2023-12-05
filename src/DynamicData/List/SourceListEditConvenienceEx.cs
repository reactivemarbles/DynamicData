// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Convenience methods for a source list.
/// </summary>
public static class SourceListEditConvenienceEx
{
    /// <summary>
    /// Adds the specified item to the source list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source list.</param>
    /// <param name="item">The item to add.</param>
    public static void Add<T>(this ISourceList<T> source, T item)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.Add(item));
    }

    /// <summary>
    /// Adds the specified items to the source list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    public static void AddRange<T>(this ISourceList<T> source, IEnumerable<T> items)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.AddRange(items));
    }

    /// <summary>
    /// Clears all items from the specified source list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source to clear.</param>
    public static void Clear<T>(this ISourceList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.Clear());
    }

    /// <summary>
    /// Loads the list with the specified items in an optimised manner i.e. calculates the differences between the old and new items
    ///  in the list and amends only the differences.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="allItems">The items to compare against and performing a delta.</param>
    /// <param name="equalityComparer">The equality comparer used to determine whether an item has changed.</param>
    public static void EditDiff<T>(this ISourceList<T> source, IEnumerable<T> allItems, IEqualityComparer<T>? equalityComparer = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        allItems.ThrowArgumentNullExceptionIfNull(nameof(allItems));

        var editDiff = new EditDiff<T>(source, equalityComparer);
        editDiff.Edit(allItems);
    }

    /// <summary>
    /// Adds the specified item to the source list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="index">The index of the item.</param>
    /// <param name="item">The item.</param>
    public static void Insert<T>(this ISourceList<T> source, int index, T item)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.Insert(index, item));
    }

    /// <summary>
    /// Inserts the elements of a collection into the <see cref="IExtendedList{T}" /> at the specified index.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="items">The items.</param>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    public static void InsertRange<T>(this ISourceList<T> source, IEnumerable<T> items, int index)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.AddRange(items, index));
    }

    /// <summary>
    /// Moves an item from the original to the destination index.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="original">The original.</param>
    /// <param name="destination">The destination.</param>
    public static void Move<T>(this ISourceList<T> source, int original, int destination)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.Move(original, destination));
    }

    /// <summary>
    /// Removes the specified item from the source list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="item">The item.</param>
    /// <returns>If the item was removed.</returns>
    public static bool Remove<T>(this ISourceList<T> source, T item)
        where T : notnull
    {
        var removed = false;
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => removed = list.Remove(item));
        return removed;
    }

    /// <summary>
    /// Removes the element at the specified index.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="index">The index.</param>
    public static void RemoveAt<T>(this ISourceList<T> source, int index)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.RemoveAt(index));
    }

    /// <summary>
    /// Removes the items from source in an optimised manner.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="itemsToRemove">The items to remove.</param>
    public static void RemoveMany<T>(this ISourceList<T> source, IEnumerable<T> itemsToRemove)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.RemoveMany(itemsToRemove));
    }

    /// <summary>
    /// Removes a range of elements from the <see cref="ISourceList{T}" />.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="index">The zero-based starting index of the range of elements to remove.</param>
    /// <param name="count">The number of elements to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> is less than 0.-or-<paramref name="count" /> is less than 0.</exception>
    /// <exception cref="ArgumentException"><paramref name="index" /> and <paramref name="count" /> do not denote a valid range of elements in the <see cref="List{T}" />.</exception>
    public static void RemoveRange<T>(this ISourceList<T> source, int index, int count)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.RemoveRange(index, count));
    }

    /// <summary>
    /// Replaces the specified original with the destination object.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="original">The original.</param>
    /// <param name="destination">The destination.</param>
    public static void Replace<T>(this ISourceList<T> source, T original, T destination)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list.Replace(original, destination));
    }

    /// <summary>
    /// Replaces the item at the specified index with the new item.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="index">The index.</param>
    /// <param name="item">The item.</param>
    public static void ReplaceAt<T>(this ISourceList<T> source, int index, T item)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        source.Edit(list => list[index] = item);
    }
}
