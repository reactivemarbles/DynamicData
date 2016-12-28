using System;
using System.Collections.Generic;
using DynamicData.Kernel;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Cache designed to be used for custom operator construction. It requires no key to be specified 
    /// but instead relies on the user specifying the key when amending data
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public sealed class IntermediateCache<TObject, TKey> : IIntermediateCache<TObject, TKey>
    {
        private readonly ObservableCache<TObject, TKey> _innnerCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntermediateCache{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public IntermediateCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _innnerCache = new ObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntermediateCache{TObject, TKey}"/> class.
        /// </summary>
        public IntermediateCache()
        {
            _innnerCache = new ObservableCache<TObject, TKey>();
        }

        #region Delegated Members

        /// <summary>
        /// Action to apply a batch update to a cache. Multiple update methods can be invoked within a single batch operation.
        /// These operations are invoked within the cache's lock and is therefore thread safe.
        /// The result of the action will produce a single changeset
        /// </summary>
        /// <param name="updateAction">The update action.</param>
        /// <param name="errorHandler">The error handler.</param>
        public void Edit(Action<ICacheUpdater<TObject, TKey>> updateAction, Action<Exception> errorHandler = null)
        {
            _innnerCache.UpdateFromIntermediate(updateAction, errorHandler);
        }

        /// <summary>
        /// A count changed observable starting with the current count
        /// </summary>
        public IObservable<int> CountChanged => _innnerCache.CountChanged;

        /// <summary>
        /// Returns a filtered changeset of cache changes preceeded with the initial state
        /// </summary>
        /// <param name="predicate">The precdicate.</param>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate)
        {
            return _innnerCache.Connect(predicate);
        }

        /// <summary>
        /// Returns a observable of cache changes preceeded with the initital cache state
        /// </summary>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return _innnerCache.Connect();
        }

        /// <summary>
        /// Returns an observable of any changes which match the specified key.  The sequence starts with the inital item in the cache (if there is one).
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _innnerCache.Watch(key);
        }

        /// <summary>
        /// The total count of cached items
        /// </summary>
        public int Count => _innnerCache.Count;

        /// <summary>
        /// Gets the Items
        /// </summary>
        public IEnumerable<TObject> Items => _innnerCache.Items;

        /// <summary>
        /// Gets the key value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _innnerCache.KeyValues;

        /// <summary>
        /// Gets the keys
        /// </summary>
        public IEnumerable<TKey> Keys => _innnerCache.Keys;

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup
        /// </remarks>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public Optional<TObject> Lookup(TKey key)
        {
            return _innnerCache.Lookup(key);
        }

        internal IChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool> filter = null)
        {
            return _innnerCache.GetInitialUpdates(filter);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            _innnerCache.Dispose();
        }

        #endregion
    }
}
