// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

namespace DynamicData.Kernel;

internal sealed class ReadOnlyCollectionLight<T> : IReadOnlyCollection<T>
{
    private readonly IList<T> _items;

    public ReadOnlyCollectionLight(IEnumerable<T> items)
    {
        _items = items.ToList();
        Count = _items.Count;
    }

    private ReadOnlyCollectionLight() => _items = new List<T>();

    public static IReadOnlyCollection<T> Empty { get; } = new ReadOnlyCollectionLight<T>();

    public int Count { get; }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
