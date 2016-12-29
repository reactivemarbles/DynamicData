using System;
using System.Collections.Generic;
using DynamicData.Kernel;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An observable cache which exposes an update API.  Used at the root
    /// of all observable chains
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class SourceCache<TObject, TKey> : ISourceCache<TObject, TKey>
    {
        private readonly ObservableCache<TObject, TKey> _innnerCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceCache{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">The key selector.</param>
        /// <exception cref="System.ArgumentNullException">keySelector</exception>
        public SourceCache(Func<TObject, TKey> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            _innnerCache = new ObservableCache<TObject, TKey>(keySelector);
        }

        #region Delegated Members

        /// <summary>
        /// Add, update and remove api via an action method. Enables the consumer to perform queries and updates
        /// safely within the innner caches lock.
        /// The result of the action will produce appropriate notifications.
        /// </summary>
        /// <param name="updateAction">The update action.</param>
        /// <param name="errorHandler">The error handler.</param>
        public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction, Action<Exception> errorHandler = null)
        {
            _innnerCache.UpdateFromSource(updateAction, errorHandler);
        }

        /// <summary>
        /// A count changed observable starting with the current count
        /// </summary>
        public IObservable<int> CountChanged => _innnerCache.CountChanged;

        /// <summary>
        /// Returns a filtered stream of cache changes preceeded with the initital filtered state
        /// </summary>
        /// <param name="predicate">The predicate.</param>
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
        public Optional<TObject> Lookup(TKey key) => _innnerCache.Lookup(key);

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
