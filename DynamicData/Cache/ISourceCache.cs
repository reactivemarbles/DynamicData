using System;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An observable cache which exposes an update API.  Used at the root
    /// of all observable chains
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface ISourceCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        /// <summary>
        /// Action to apply a batch update to a cache. Multiple update methods can be invoked within a single batch operation.
        /// These operations are invoked within the cache's lock and is therefore thread safe.
        /// The result of the action will produce a single changeset
        /// </summary>
        /// <param name="updateAction">The update action.</param>
        /// <param name="errorHandler">The error handler.</param>
        void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction, Action<Exception> errorHandler = null);
    }
}
