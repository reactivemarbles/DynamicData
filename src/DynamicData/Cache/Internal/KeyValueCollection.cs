// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the KeyValueCollection class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class KeyValueCollection<TObject, TKey> : IKeyValueCollection<TObject, TKey>
{
    /// <summary>
    /// The _items field.
    /// </summary>
    private readonly IReadOnlyCollection<KeyValuePair<TKey, TObject>> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyValueCollection{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <param name="sortReason">The sortReason value.</param>
    /// <param name="optimisations">The optimisations value.</param>
    public KeyValueCollection(IReadOnlyCollection<KeyValuePair<TKey, TObject>> items, IComparer<KeyValuePair<TKey, TObject>> comparer, SortReason sortReason, SortOptimisations optimisations)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Comparer = comparer;
        SortReason = sortReason;
        Optimisations = optimisations;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyValueCollection{TObject, TKey}"/> class.
    /// </summary>
    public KeyValueCollection()
    {
        Optimisations = SortOptimisations.None;
        _items = new List<KeyValuePair<TKey, TObject>>();
        Comparer = new KeyValueComparer<TObject, TKey>();
    }

    /// <summary>
    /// Gets the comparer used to perform the sort.
    /// </summary>
    /// <value>
    /// The comparer.
    /// </value>
    public IComparer<KeyValuePair<TKey, TObject>> Comparer { get; }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the Optimisations value.
    /// </summary>
    public SortOptimisations Optimisations { get; }

    /// <summary>
    /// Gets the SortReason value.
    /// </summary>
    public SortReason SortReason { get; }

    /// <summary>
    /// Gets or sets the indexed value.
    /// </summary>
    /// <param name="index">The index value.</param>
    public KeyValuePair<TKey, TObject> this[int index] => _items.ElementAt(index);

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IEnumerator<KeyValuePair<TKey, TObject>> GetEnumerator() => _items.GetEnumerator();

    /// <summary>
    /// Executes the GetEnumerator operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
