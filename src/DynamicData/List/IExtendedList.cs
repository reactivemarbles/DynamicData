// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Represents a list which supports range operations.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IExtendedList<T> : IList<T>
{
    /// <summary>
    /// Adds the elements of the specified collection to the end of the collection.
    /// </summary>
    /// <param name="collection">The items to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection" /> is null.</exception>
    void AddRange(IEnumerable<T> collection);

    /// <summary>
    /// Inserts the elements of a collection into the <c>List&lt;T&gt;</c> at the specified index.
    /// </summary>
    /// <param name="collection">The items to insert.</param>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> is less than 0.-or-<paramref name="index" /> is greater than <c>List&lt;T&gt;.Count</c>.</exception>
    void InsertRange(IEnumerable<T> collection, int index);

    /// <summary>
    /// Moves an item from the original to the destination index.
    /// </summary>
    /// <param name="original">The original.</param>
    /// <param name="destination">The destination.</param>
    void Move(int original, int destination);

    /// <summary>
    /// Removes a range of elements from the <c>List&lt;T&gt;</c>.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range of elements to remove.</param><param name="count">The number of elements to remove.</param><exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.-or-<paramref name="count"/> is less than 0.</exception><exception cref="ArgumentException"><paramref name="index"/> and <paramref name="count"/> do not denote a valid range of elements in the <c>List&lt;T&gt;</c>.</exception>
    void RemoveRange(int index, int count);
}
