// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

internal class SortedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, ISortedChangeSet<TObject, TKey>
    where TKey : notnull
{
    public static readonly new ISortedChangeSet<TObject, TKey> Empty = new SortedChangeSet<TObject, TKey>();

    public SortedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates)
        : base(updates)
    {
        SortedItems = sortedItems;
    }

    private SortedChangeSet()
    {
        SortedItems = new KeyValueCollection<TObject, TKey>();
    }

    public IKeyValueCollection<TObject, TKey> SortedItems { get; }

    public bool Equals(SortedChangeSet<TObject, TKey> other)
    {
        return SortedItems.SequenceEqual(other.SortedItems);
    }

    public override bool Equals(object? obj)
    {
        return obj is SortedChangeSet<TObject, TKey> value && Equals(value);
    }

    public override int GetHashCode()
    {
        return SortedItems.GetHashCode();
    }

    public override string ToString()
    {
        return $"SortedChangeSet. Count= {SortedItems.Count}. Updates = {Count}";
    }
}
