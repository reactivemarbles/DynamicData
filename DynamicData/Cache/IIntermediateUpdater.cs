using System.Collections.Generic;

namespace DynamicData
{
    /// <summary>
    /// Api for updating  an intermediate cache
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
    public interface IIntermediateUpdater<TObject, TKey> : IQuery<TObject, TKey> //:IUpdater<TObject,TKey>
    {
        /// <summary>
        /// Adds or updates the specified item / key pair
        /// </summary>
        void AddOrUpdate(TObject item, TKey key);

        /// <summary>
        /// Sends a signal for operators to recalculate it's state 
        /// </summary>
        void Evaluate();

        /// <summary>
        /// Evaluates the items matching the specified keys
        /// </summary>
        /// <param name="keys">The keys.</param>
        void Evaluate(IEnumerable<TKey> keys);

        /// <summary>
        /// Evaluates the item matching the specified key
        /// </summary>
        void Evaluate(TKey key);

        /// <summary>
        ///Removes the specified keys
        /// </summary>
        void Remove(IEnumerable<TKey> keys);

        /// <summary>
        ///Remove the specified keys
        /// </summary>
        void Remove(TKey key);

        /// <summary>
        /// Updates using changes using the specified changeset
        /// </summary>
        void Update(IChangeSet<TObject, TKey> changes);

        /// <summary>
        /// Clears all items from the underlying cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Acccummulation of updates since last call of this method
        /// </summary>
        /// <returns></returns>
        IChangeSet<TObject, TKey> AsChangeSet();
    }
}
