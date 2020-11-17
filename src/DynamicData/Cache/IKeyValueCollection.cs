// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A key collection which contains sorting information.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IKeyValueCollection<TObject, TKey> : IEnumerable<KeyValuePair<TKey, TObject>>
    {
        /// <summary>
        /// Gets the comparer used to peform the sort.
        /// </summary>
        /// <value>
        /// The comparer.
        /// </value>
        IComparer<KeyValuePair<TKey, TObject>> Comparer { get; }

        /// <summary>
        /// Gets the count of items.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        int Count { get; }

        /// <summary>
        /// Gets the optimisations used to produce the sort.
        /// </summary>
        /// <value>
        /// The optimisations.
        /// </value>
        SortOptimisations Optimisations { get; }

        /// <summary>
        /// Gets the reason for a sort being applied.
        /// </summary>
        /// <value>
        /// The sort reason.
        /// </value>
        SortReason SortReason { get; }

        /// <summary>
        /// Gets the element at the specified index in the read-only list.
        /// </summary>
        ///
        /// <returns>
        /// The element at the specified index in the read-only list.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get. </param>
        /// <returns>The key value pair.</returns>
        KeyValuePair<TKey, TObject> this[int index] { get; }
    }
}