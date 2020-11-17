// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A collection of changes.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class ChangeSet<TObject, TKey> : List<Change<TObject, TKey>>, IChangeSet<TObject, TKey>
        where TKey : notnull
    {
        /// <summary>
        /// An empty change set.
        /// </summary>
        public static readonly ChangeSet<TObject, TKey> Empty = new ChangeSet<TObject, TKey>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
        /// </summary>
        public ChangeSet()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="collection">The collection of items to start the change set with.</param>
        public ChangeSet(IEnumerable<Change<TObject, TKey>> collection)
            : base(collection)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSet{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="capacity">The initial capacity of the change set.</param>
        public ChangeSet(int capacity)
            : base(capacity)
        {
        }

        /// <inheritdoc />
        public int Adds => this.Count(c => c.Reason == ChangeReason.Add);

        /// <inheritdoc />
        public int Moves => this.Count(c => c.Reason == ChangeReason.Moved);

        /// <inheritdoc />
        public int Refreshes => this.Count(c => c.Reason == ChangeReason.Refresh);

        /// <inheritdoc />
        public int Removes => this.Count(c => c.Reason == ChangeReason.Remove);

        /// <inheritdoc />
        public int Updates => this.Count(c => c.Reason == ChangeReason.Update);

        /// <inheritdoc />
        public override string ToString()
        {
            return $"ChangeSet<{typeof(TObject).Name}.{typeof(TKey).Name}>. Count={Count}";
        }
    }
}