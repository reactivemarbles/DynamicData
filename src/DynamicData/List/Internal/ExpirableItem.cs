// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

internal sealed class ExpirableItem<TObject>(TObject value, DateTime dateTime, long index) : IEquatable<ExpirableItem<TObject>>
{
    public DateTime ExpireAt { get; } = dateTime;

    public long Index { get; } = index;

    public TObject Item { get; } = value;

    public static bool operator ==(ExpirableItem<TObject> left, ExpirableItem<TObject> right) => Equals(left, right);

    public static bool operator !=(ExpirableItem<TObject> left, ExpirableItem<TObject> right) => !Equals(left, right);

    public bool Equals(ExpirableItem<TObject>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EqualityComparer<TObject>.Default.Equals(Item, other.Item) && ExpireAt.Equals(other.ExpireAt) && Index == other.Index;
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

        return obj is ExpirableItem<TObject> item && Equals(item);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item);
            hashCode = (hashCode * 397) ^ ExpireAt.GetHashCode();
            hashCode = (hashCode * 397) ^ Index.GetHashCode();
            return hashCode;
        }
    }

    public override string ToString() => $"{Item} @ {ExpireAt}";
}
