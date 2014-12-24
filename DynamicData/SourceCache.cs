using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
    /// <summary>
    /// An observable cache which exposes an update API.  Used at the root
    /// of all observable chains
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public  class SourceCache<TObject, TKey> : ISourceCache<TObject, TKey> 
    {
        private readonly ObservableCache<TObject, TKey> _innnerCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceCache{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">The key selector.</param>
        /// <exception cref="System.ArgumentNullException">keySelector</exception>
        public SourceCache(Func<TObject, TKey> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException("keySelector");
            _innnerCache = new ObservableCache<TObject, TKey>(keySelector);  
        }
        
        #region Delegated Members
        

        /// <summary>
        /// Add, update and remove api via an action method. Enables the consumer to perform queries and updates
        /// safely within the innner caches lock.
        /// 
        /// The result of the action will produce appropriate notifications.
        /// </summary>
        /// <param name="updateAction">The update action.</param>
        public void BatchUpdate(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            _innnerCache.UpdateFromSource(updateAction);
        }

        /// <summary>
        /// Returns a filtered stream of cache changes preceeded with the initital filtered state
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <param name="parallelisationOptions">Option to parallise the filter operation  Only applies if the filter parameter is not null</param>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions = null)
        {
            return _innnerCache.Connect(filter, parallelisationOptions);
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
        /// Returns an observable of any changes which match the specified key,  preceeded with the initital cache state
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
        public int Count
        {
            get { return _innnerCache.Count; }
        }

        /// <summary>
        /// Gets the Items
        /// </summary>
        public IEnumerable<TObject> Items
        {
            get { return _innnerCache.Items; }
        }

        /// <summary>
        /// Gets the key value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<TKey,TObject>> KeyValues
        {
            get { return _innnerCache.KeyValues; }
        }

        /// <summary>
        /// Gets the keys
        /// </summary>
        public IEnumerable<TKey> Keys
        {
            get { return _innnerCache.Keys; }
        }

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


        public void Dispose()
        {
            _innnerCache.Dispose();
        }

        #endregion
    }
}