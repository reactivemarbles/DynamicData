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
        private readonly ObservableCache<TObject, TKey> _innerCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceCache{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">The key selector.</param>
        /// <exception cref="System.ArgumentNullException">keySelector</exception>
        public SourceCache(Func<TObject, TKey> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            _innerCache = new ObservableCache<TObject, TKey>(keySelector);
        }

        #region Delegated Members

        /// <inheritdoc />
        /// <summary>
        /// Add, update and remove api via an action method. Enables the consumer to perform queries and updates
        /// safely within the innner caches lock.
        /// The result of the action will produce appropriate notifications.
        /// </summary>
        /// <param name="updateAction">The update action.</param>
        public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            _innerCache.UpdateFromSource(updateAction);
        }

        /// <summary>
        /// Notifies the observer that the source list has finished sending notifications.
        /// </summary>
        public void OnCompleted()
        {
            (_innerCache as ICollectionSubject)?.OnCompleted();
        }

        /// <summary>
        /// Notifies the observer that the source list has experienced an error condition.
        /// </summary>
        public void OnError(Exception exception)
        {
            (_innerCache as ICollectionSubject)?.OnCompleted();
        }

        /// <inheritdoc />
        public IObservable<int> CountChanged => _innerCache.CountChanged;

        /// <summary>
        /// Returns a filtered stream of cache changes preceeded with the initital filtered state
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate)
        {
            return _innerCache.Connect(predicate);
        }

        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return _innerCache.Connect();
        }

        /// <summary>
        /// Returns an observable of any changes which match the specified key,  preceeded with the initital cache state
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _innerCache.Watch(key);
        }

        /// <summary>
        /// The total count of cached items
        /// </summary>
        public int Count => _innerCache.Count;

        /// <summary>
        /// Gets the Items
        /// </summary>
        public IEnumerable<TObject> Items => _innerCache.Items;

        /// <summary>
        /// Gets the key value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _innerCache.KeyValues;

        /// <summary>
        /// Gets the keys
        /// </summary>
        public IEnumerable<TKey> Keys => _innerCache.Keys;

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup
        /// </remarks>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            _innerCache.Dispose();
        }

        #endregion
    }
}
