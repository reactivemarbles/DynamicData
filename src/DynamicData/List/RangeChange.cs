// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Multiple change container.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public sealed class RangeChange<T> : IEnumerable<T>
{
    private readonly List<T> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="RangeChange{T}"/> class.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="index">The index.</param>
    public RangeChange(IEnumerable<T> items, int index = -1)
    {
        Index = index;
        _items = items.AsList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RangeChange{T}"/> class.
    /// </summary>
    private RangeChange()
    {
        _items = new List<T>();
        Index = -1;
    }

    /// <summary>
    /// Gets a Empty version of the RangeChange.
    /// </summary>
    public static RangeChange<T> Empty { get; } = new();

    /// <summary>
    ///     Gets the total update count.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the index initial index i.e. for the initial starting point of the range insertion.
    /// </summary>
    /// <value>
    /// The index.
    /// </value>
    public int Index { get; private set; }

    /// <summary>
    /// Adds the specified item to the range.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(T item) => _items.Add(item);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <summary>
    /// Inserts the  item in the range at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="item">The item.</param>
    public void Insert(int index, T item) => _items.Insert(index, item);

    /// <summary>
    /// Sets the index of the starting index of the range.
    /// </summary>
    /// <param name="index">The index.</param>
    public void SetStartingIndex(int index) => Index = index;

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString() => $"Range<{typeof(T).Name}>. Count={Count}";

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
