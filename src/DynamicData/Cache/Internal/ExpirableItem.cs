// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal readonly struct ExpirableItem<TObject, TKey>(TObject value, TKey key, DateTime dateTime, long index = 0) : IEquatable<ExpirableItem<TObject, TKey>>
{
    public TObject Value { get; } = value;

    public TKey Key { get; } = key;

    public DateTime ExpireAt { get; } = dateTime;

    public long Index { get; } = index;

    public static bool operator ==(in ExpirableItem<TObject, TKey> left, in ExpirableItem<TObject, TKey> right) => left.Equals(right);

    public static bool operator !=(in ExpirableItem<TObject, TKey> left, in ExpirableItem<TObject, TKey> right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(ExpirableItem<TObject, TKey> other) => EqualityComparer<TKey>.Default.Equals(Key, other.Key) && ExpireAt.Equals(other.ExpireAt);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ExpirableItem<TObject, TKey> expItem && Equals(expItem);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Key is null ? 0 : EqualityComparer<TKey>.Default.GetHashCode(Key) * 397) ^ ExpireAt.GetHashCode();
        }
    }

    public override string ToString() => $"Key: {Key}, Expire At: {ExpireAt}";
}
