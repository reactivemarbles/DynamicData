using System.Collections.Generic;

namespace DynamicData
{
    /// <summary>
    /// Api for updating  a source cache
    /// 
    /// Use batch update to produce singular changeset.
    /// 
    /// NB:The evaluate method is used to signal to any observing operators
    /// to  reevaluate whether the the object still matches downstream operators.
    /// This is primarily targeted to inline object changes such as datetime and calculated fields.
    /// 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface ISourceUpdater<TObject, TKey> : IQuery<TObject, TKey>
    {
        /// <summary>
        /// Clears existing values and loads the sepcified items
        /// </summary>
        /// <param name="items">The items.</param>
        void Load(IEnumerable<TObject> items);

        /// <summary>
        /// Adds or changes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void AddOrUpdate(IEnumerable<TObject> items);

        /// <summary>
        /// Adds or update the item, 
        /// </summary>
        /// <param name="item">The item.</param>
        void AddOrUpdate(TObject item);

        /// <summary>
        /// Evaluates the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void Evaluate(IEnumerable<TObject> items);

        /// <summary>
        ///Evaluates the specified item
        /// </summary>
        /// <param name="item">The item.</param>
        void Evaluate(TObject item);

        /// <summary>
        /// Evaluates the items matching the specified keys
        /// </summary>
        /// <param name="keys">The keys.</param>
        void Evaluate(IEnumerable<TKey> keys);

        /// <summary>
        /// Evaluates the item matching the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        void Evaluate(TKey key);

        /// <summary>
        /// Sends a signal for operators to recalculate it's state 
        /// </summary>
        void Evaluate();

        /// <summary>
        ///Removes the specified items
        /// </summary>
        /// <param name="items">The items.</param>
        void Remove(IEnumerable<TObject> items);

        /// <summary>
        /// Removes the items matching the specified keys
        /// </summary>
        /// <param name="keys">The keys.</param>
        void Remove(IEnumerable<TKey> keys);

        /// <summary>
        /// Overload of remove due to ambiguous method when TObject and TKey are of the same type
        /// </summary>
        /// <param name="key">The key.</param>
        void RemoveKeys(IEnumerable<TKey> key);

        /// <summary>
        /// Removes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        void Remove(TObject item);

        /// <summary>
        /// Remove the item with the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        void Remove(TKey key);

        /// <summary>
        /// Overload of remove due to ambiguous method when TObject and TKey are of the same type
        /// </summary>
        /// <param name="key">The key.</param>
        void RemoveKey(TKey key);

        /// <summary>
        /// Clears all items from the underlying cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Updates using changes using the specified changeset
        /// </summary>
        void Update(IChangeSet<TObject, TKey> changes);
    }
}
