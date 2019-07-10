// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Aggregation
{
    /// <summary>
    /// An object representing added and removed items in a continuous aggregation stream
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    public readonly struct AggregateItem<TObject> : IEquatable<AggregateItem<TObject>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateItem{TObject}"/> struct.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="item">The item.</param>
        public AggregateItem(AggregateType type, TObject item)
        {
            Type = type;
            Item = item;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        public AggregateType Type { get; }

        /// <summary>
        /// Gets the item.
        /// </summary>
        public TObject Item { get; }

        public override bool Equals(object obj)
        {
            return obj is AggregateItem<TObject> item && Equals(item);
        }

        public bool Equals(AggregateItem<TObject> other)
        {
            return Type == other.Type &&
                   EqualityComparer<TObject>.Default.Equals(Item, other.Item);
        }

        public override int GetHashCode()
        {
            var hashCode = -1719135621;
            hashCode = hashCode * -1521134295 + Type.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<TObject>.Default.GetHashCode(Item);
            return hashCode;
        }

        public static bool operator ==(AggregateItem<TObject> left, AggregateItem<TObject> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AggregateItem<TObject> left, AggregateItem<TObject> right)
        {
            return !(left == right);
        }
    }
}
