// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class ImmutableGroup<TObject, TGroupKey> : IGrouping<TObject, TGroupKey>, IEquatable<ImmutableGroup<TObject, TGroupKey>>
{
    private readonly IReadOnlyCollection<TObject> _items;

    internal ImmutableGroup(TGroupKey key, IList<TObject> items)
    {
        Key = key;
        _items = new ReadOnlyCollectionLight<TObject>(items);
    }

    public int Count => _items.Count;

    public IEnumerable<TObject> Items => _items;

    public TGroupKey Key { get; }

    public static bool operator ==(ImmutableGroup<TObject, TGroupKey> left, ImmutableGroup<TObject, TGroupKey> right) => Equals(left, right);

    public static bool operator !=(ImmutableGroup<TObject, TGroupKey> left, ImmutableGroup<TObject, TGroupKey> right) => !Equals(left, right);

    public bool Equals(ImmutableGroup<TObject, TGroupKey>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ImmutableGroup<TObject, TGroupKey> value && Equals(value);
    }

    public override int GetHashCode() => Key is null ? 0 : EqualityComparer<TGroupKey>.Default.GetHashCode(Key);

    public override string ToString() => $"Grouping for: {Key} ({Count} items)";
}
