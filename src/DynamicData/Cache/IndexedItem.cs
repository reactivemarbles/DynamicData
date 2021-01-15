// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An item with it's index.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public sealed class IndexedItem<TObject, TKey> : IEquatable<IndexedItem<TObject, TKey>> // : IIndexedItem<TObject, TKey>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IndexedItem{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        public IndexedItem(TObject value, TKey key, int index)
        {
            Index = index;
            Value = value;
            Key = key;
        }

        /// <summary>
        /// Gets the index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public TObject Value { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is IndexedItem<TObject, TKey> key && Equals(key);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Key is null ? 0 : EqualityComparer<TKey>.Default.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ (Value is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Value));
                hashCode = (hashCode * 397) ^ Index;
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"Value: {Value}, Key: {Key}, CurrentIndex: {Index}";

        /// <inheritdoc />
        public bool Equals(IndexedItem<TObject, TKey>? other)
        {
            if (other is null)
            {
                return false;
            }

            return EqualityComparer<TKey?>.Default.Equals(Key, other.Key) && EqualityComparer<TObject?>.Default.Equals(Value, other.Value) && Index == other.Index;
        }
    }
}