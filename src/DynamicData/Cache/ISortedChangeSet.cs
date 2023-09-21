// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// An update collection as per the system convention additionally providing a sorted set of the underling state.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface ISortedChangeSet<TObject, TKey> : IChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Gets all cached items in sort order.
    /// </summary>
    IKeyValueCollection<TObject, TKey> SortedItems { get; }
}
