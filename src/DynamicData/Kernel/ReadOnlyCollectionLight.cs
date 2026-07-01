// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Kernel;
#else

namespace DynamicData.Kernel;
#endif

/// <summary>
/// Provides members for the ReadOnlyCollectionLight class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class ReadOnlyCollectionLight<T> : IReadOnlyCollection<T>
{
    /// <summary>
    /// The _items field.
    /// </summary>
    private readonly IList<T> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyCollectionLight{T}"/> class.
    /// </summary>
    /// <param name="items">The items value.</param>
    public ReadOnlyCollectionLight(IEnumerable<T> items)
    {
        _items = items.ToList();
        Count = _items.Count;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyCollectionLight{T}"/> class.
    /// </summary>
    private ReadOnlyCollectionLight() => _items = new List<T>();

    /// <summary>
    /// Gets the Empty value.
    /// </summary>
    public static IReadOnlyCollection<T> Empty { get; } = new ReadOnlyCollectionLight<T>();

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
