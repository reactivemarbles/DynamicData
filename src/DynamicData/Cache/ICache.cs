// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// <para>A cache which captures all changes which are made to it. These changes are recorded until CaptureChanges() at which point thw changes are cleared.</para>
/// <para>Used for creating custom operators.</para>
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <seealso cref="IQuery{TObject, TKey}" />
public interface ICache<TObject, TKey> : IQuery<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Adds or updates the item using the specified key.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="key">The key.</param>
    void AddOrUpdate(TObject item, TKey key);

    /// <summary>
    /// Clears all items.
    /// </summary>
    void Clear();

    /// <summary>
    /// Clones the cache from the specified changes.
    /// </summary>
    /// <param name="changes">The changes.</param>
    void Clone(IChangeSet<TObject, TKey> changes);

    /// <summary>
    /// Sends a signal for operators to recalculate it's state.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Refreshes the items matching the specified keys.
    /// </summary>
    /// <param name="keys">The keys.</param>
    void Refresh(IEnumerable<TKey> keys);

    /// <summary>
    /// Refreshes the item matching the specified key.
    /// </summary>
    /// <param name="key">The key to refresh.</param>
    void Refresh(TKey key);

    /// <summary>
    /// Removes the item matching the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    void Remove(TKey key);

    /// <summary>
    /// Removes all items matching the specified keys.
    /// </summary>
    /// <param name="keys">The keys to remove.</param>
    void Remove(IEnumerable<TKey> keys);
}
