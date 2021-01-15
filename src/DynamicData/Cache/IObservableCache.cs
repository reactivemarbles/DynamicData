// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A cache for observing and querying in memory data. With additional data access operators.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IObservableCache<TObject, TKey> : IConnectableCache<TObject, TKey>, IDisposable
        where TKey : notnull
    {
        /// <summary>
        /// Gets the total count of cached items.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the Items.
        /// </summary>
        IEnumerable<TObject> Items { get; }

        /// <summary>
        /// Gets the keys.
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        /// <summary>
        /// Gets the key value pairs.
        /// </summary>
        IEnumerable<KeyValuePair<TKey, TObject>> KeyValues { get; }

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup.
        /// </remarks>
        /// <param name="key">The key.</param>
        /// <returns>An optional with the looked up value.</returns>
        Optional<TObject> Lookup(TKey key);
    }
}