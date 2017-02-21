// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A cache which captures all changes which are made to it. These changes are recorded until CaptureChanges() at which point thw changes are cleared.
    /// 
    /// Used for creating custom operators
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <seealso cref="DynamicData.IQuery{TObject, TKey}" />
    public interface ICache<TObject, TKey> : IQuery<TObject, TKey>
    {
        /// <summary>
        /// Clones the cache from the specified changes
        /// </summary>
        /// <param name="changes">The changes.</param>
        void Clone(IChangeSet<TObject, TKey> changes);
       
        /// <summary>
        /// Adds or updates the item using the specified key
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="key">The key.</param>
        void AddOrUpdate(TObject item, TKey key);


        /// <summary>
        /// Removes the item matching the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        void Remove(TKey key);
       
        /// <summary>
        /// Clears all items
        /// </summary>
        void Clear();
    }
}
