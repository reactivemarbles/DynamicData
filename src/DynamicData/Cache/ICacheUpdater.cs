// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// <para>Api for updating  an intermediate cache.</para>
/// <para>Use edit to produce singular change set.</para>
/// <para>
/// NB:The evaluate method is used to signal to any observing operators
/// to  reevaluate whether the object still matches downstream operators.
/// This is primarily targeted to inline object changes such as datetime and calculated fields.
/// </para>
///
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface ICacheUpdater<TObject, TKey> : IQuery<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Adds or updates the specified key value pairs.
    /// </summary>
    /// <param name="keyValuePairs">The key value pairs to add or update.</param>
    void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> keyValuePairs);

    /// <summary>
    /// Adds or updates the specified key value pair.
    /// </summary>
    /// <param name="item">The key value pair to add or update.</param>
    void AddOrUpdate(KeyValuePair<TKey, TObject> item);

    /// <summary>
    /// Adds or updates the specified item / key pair.
    /// </summary>
    /// <param name="item">The item to add or update.</param>
    /// <param name="key">The key to add or update.</param>
    void AddOrUpdate(TObject item, TKey key);

    /// <summary>
    /// Clears all items from the underlying cache.
    /// </summary>
    void Clear();

    /// <summary>
    /// Clones the change set to the cache.
    /// </summary>
    /// <param name="changes">The changes to clone.</param>
    void Clone(IChangeSet<TObject, TKey> changes);

    /// <summary>
    /// Gets the key associated with the object.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The key for the specified object.</returns>
    TKey GetKey(TObject item);

    /// <summary>
    /// Gets the key values for the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>An enumeration of key value pairs of the key and the object.</returns>
    IEnumerable<KeyValuePair<TKey, TObject>> GetKeyValues(IEnumerable<TObject> items);

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
    /// Removes the specified keys.
    /// </summary>
    /// <param name="keys">The keys to remove the values for.</param>
    void Remove(IEnumerable<TKey> keys);

    /// <summary>
    /// Remove the specified keys.
    /// </summary>
    /// <param name="key">The key of the item to remove.</param>
    void Remove(TKey key);

    /// <summary>
    /// Removes the specified  key value pairs.
    /// </summary>
    /// <param name="items">The key value pairs to remove.</param>
    void Remove(IEnumerable<KeyValuePair<TKey, TObject>> items);

    /// <summary>
    /// Removes the specified key value pair.
    /// </summary>
    /// <param name="item">The key value pair to remove.</param>
    void Remove(KeyValuePair<TKey, TObject> item);

    /// <summary>
    /// Overload of remove due to ambiguous method when TObject and TKey are of the same type.
    /// </summary>
    /// <param name="key">The key.</param>
    void RemoveKey(TKey key);

    /// <summary>
    /// Overload of remove due to ambiguous method when TObject and TKey are of the same type.
    /// </summary>
    /// <param name="key">The key.</param>
    void RemoveKeys(IEnumerable<TKey> key);

    /// <summary>
    /// Updates using changes using the specified change set.
    /// </summary>
    /// <param name="changes">The changes to update with.</param>
    [Obsolete("Use Clone()")]
    void Update(IChangeSet<TObject, TKey> changes);
}
