// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Exposes internal cache state to enable querying.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IQuery<TObject, TKey>
    where TObject : notnull
{
    /// <summary>
    /// Gets the count.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the items.
    /// </summary>
    IEnumerable<TObject> Items { get; }

    /// <summary>
    /// Gets the keys.
    /// </summary>
    IEnumerable<TKey> Keys { get; }

    /// <summary>
    /// Gets the items together with their keys.
    /// </summary>
    /// <value>
    /// The key values.
    /// </value>
    IEnumerable<KeyValuePair<TKey, TObject>> KeyValues { get; }

    /// <summary>
    /// Lookup a single item using the specified key.
    /// </summary>
    /// <remarks>
    /// Fast indexed lookup.
    /// </remarks>
    /// <param name="key">The key.</param>
    /// <returns>The looked up value.</returns>
    Optional<TObject> Lookup(TKey key);
}
