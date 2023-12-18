// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

namespace DynamicData.Cache.Internal;

internal sealed class KeyValueCollection<TObject, TKey> : IKeyValueCollection<TObject, TKey>
{
    private readonly IReadOnlyCollection<KeyValuePair<TKey, TObject>> _items;

    public KeyValueCollection(IReadOnlyCollection<KeyValuePair<TKey, TObject>> items, IComparer<KeyValuePair<TKey, TObject>> comparer, SortReason sortReason, SortOptimisations optimisations)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Comparer = comparer;
        SortReason = sortReason;
        Optimisations = optimisations;
    }

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

    public int Count => _items.Count;

    public SortOptimisations Optimisations { get; }

    public SortReason SortReason { get; }

    public KeyValuePair<TKey, TObject> this[int index] => _items.ElementAt(index);

    public IEnumerator<KeyValuePair<TKey, TObject>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
