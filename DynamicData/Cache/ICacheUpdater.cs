using System;
using System.Collections.Generic;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Api for updating  an intermediate cache
    /// 
    /// Use edit to produce singular changeset.
    /// 
    /// NB:The evaluate method is used to signal to any observing operators
    /// to  reevaluate whether the the object still matches downstream operators.
    /// This is primarily targeted to inline object changes such as datetime and calculated fields.
    /// 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface ICacheUpdater<TObject, TKey> : IQuery<TObject, TKey>
    {
        /// <summary>
        /// Adds or updates the specified  key value pairs 
        /// </summary>
        void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> keyValuePairs);

        /// <summary>
        /// Adds or updates the specified key value pair
        /// </summary>
        void AddOrUpdate(KeyValuePair<TKey, TObject> item);

        /// <summary>
        /// Adds or updates the specified item / key pair
        /// </summary>
        void AddOrUpdate(TObject item, TKey key);

        /// <summary>
        /// Sends a signal for operators to recalculate it's state 
        /// </summary>
        void Refresh();

        /// <summary>
        /// Refreshes the items matching the specified keys
        /// </summary>
        /// <param name="keys">The keys.</param>
        void Refresh(IEnumerable<TKey> keys);

        /// <summary>
        /// Refreshes the item matching the specified key
        /// </summary>
        void Refresh(TKey key);

        /// <summary>
        /// Sends a signal for operators to recalculate it's state 
        /// </summary>
        [Obsolete(Constants.EvaluateIsDead)]
        void Evaluate();

        /// <summary>
        /// Refreshes the items matching the specified keys
        /// </summary>
        /// <param name="keys">The keys.</param>
        [Obsolete(Constants.EvaluateIsDead)]
        void Evaluate(IEnumerable<TKey> keys);

        /// <summary>
        /// Refreshes the item matching the specified key
        /// </summary>
        [Obsolete(Constants.EvaluateIsDead)]
        void Evaluate(TKey key);

        /// <summary>
        ///Removes the specified keys
        /// </summary>
        void Remove(IEnumerable<TKey> keys);

        /// <summary>
        /// Overload of remove due to ambiguous method when TObject and TKey are of the same type
        /// </summary>
        /// <param name="key">The key.</param>
        void RemoveKeys(IEnumerable<TKey> key);

        /// <summary>
        ///Remove the specified keys
        /// </summary>
        void Remove(TKey key);

        /// <summary>
        /// Overload of remove due to ambiguous method when TObject and TKey are of the same type
        /// </summary>
        /// <param name="key">The key.</param>
        void RemoveKey(TKey key);

        /// <summary>
        /// Removes the specified  key value pairs 
        /// </summary>
        void Remove(IEnumerable<KeyValuePair<TKey, TObject>> items);

        /// <summary>
        ///Removes the specified key value pair
        /// </summary>
        void Remove(KeyValuePair<TKey, TObject> item);

        /// <summary>
        /// Updates using changes using the specified changeset
        /// </summary>
        void Update(IChangeSet<TObject, TKey> changes);

        /// <summary>
        /// Clears all items from the underlying cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the key associated with the object
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        TKey GetKey(TObject item);

        /// <summary>
        /// Gets the key values for the specified items
        /// </summary>
        /// <param name="items">The items.</param>
        /// <returns></returns>
        IEnumerable<KeyValuePair<TKey, TObject>> GetKeyValues(IEnumerable<TObject> items);
    }
}
