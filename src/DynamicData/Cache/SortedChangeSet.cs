// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

internal sealed class SortedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, ISortedChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public static new readonly ISortedChangeSet<TObject, TKey> Empty = new SortedChangeSet<TObject, TKey>();

    public SortedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates)
        : base(updates) => SortedItems = sortedItems;

    private SortedChangeSet() => SortedItems = new KeyValueCollection<TObject, TKey>();

    public IKeyValueCollection<TObject, TKey> SortedItems { get; }

    public bool Equals(SortedChangeSet<TObject, TKey> other) => SortedItems.SequenceEqual(other.SortedItems);

    public override bool Equals(object? obj) => obj is SortedChangeSet<TObject, TKey> value && Equals(value);

    public override int GetHashCode() => SortedItems.GetHashCode();

    public override string ToString() => $"SortedChangeSet. Count= {SortedItems.Count}. Updates = {Count}";
}
