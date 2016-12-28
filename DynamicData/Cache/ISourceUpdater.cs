using System.Collections.Generic;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Api for updating  a source cache
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
    public interface ISourceUpdater<TObject, TKey> : ICacheUpdater<TObject, TKey>
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
        ///Removes the specified items
        /// </summary>
        /// <param name="items">The items.</param>
        void Remove(IEnumerable<TObject> items);


        /// <summary>
        /// Removes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        void Remove(TObject item);
    }
}
