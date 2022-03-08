// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Cache.Internal;

internal readonly struct ExpirableItem<TObject, TKey> : IEquatable<ExpirableItem<TObject, TKey>>
{
    public ExpirableItem(TObject value, TKey key, DateTime dateTime, long index = 0)
    {
        Value = value;
        Key = key;
        ExpireAt = dateTime;
        Index = index;
    }

    public TObject Value { get; }

    public TKey Key { get; }

    public DateTime ExpireAt { get; }

    public long Index { get; }

    public static bool operator ==(ExpirableItem<TObject, TKey> left, ExpirableItem<TObject, TKey> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ExpirableItem<TObject, TKey> left, ExpirableItem<TObject, TKey> right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc />
    public bool Equals(ExpirableItem<TObject, TKey> other)
    {
        return EqualityComparer<TKey>.Default.Equals(Key, other.Key) && ExpireAt.Equals(other.ExpireAt);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ExpirableItem<TObject, TKey> value && Equals(value);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Key is null ? 0 : EqualityComparer<TKey>.Default.GetHashCode(Key) * 397) ^ ExpireAt.GetHashCode();
        }
    }

    public override string ToString()
    {
        return $"Key: {Key}, Expire At: {ExpireAt}";
    }
}
