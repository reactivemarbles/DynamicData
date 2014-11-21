using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    internal sealed class AnomynousObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        private readonly IObservableCache<TObject, TKey> _cache;


        public AnomynousObservableCache(IObservable<IChangeSet<TObject,TKey>> source)
        {
            _cache = new ObservableCache<TObject,TKey>(source);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public AnomynousObservableCache(IObservableCache<TObject, TKey> cache)
        {
            _cache = new ObservableCache<TObject, TKey>(cache.Connect());
        }

        #region Delgated Members


        /// <summary>
        /// Watches updates from a single item using the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _cache.Watch(key);
        }

        /// <summary>
        /// Returns a stream of cache updates preceeded with the initital cache state
        /// </summary>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return _cache.Connect();
        }

        /// <summary>
        /// Returns a filtered stream of cache updates preceeded with the initital filtered state
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <param name="parallelisationOptions">Option to parallise the filter operation  Only applies if the filter parameter is not null</param>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions = null)
        {
            return _cache.Connect(filter, parallelisationOptions);
        }

        /// <summary>
        /// Gets the keys
        /// </summary>
        public IEnumerable<TKey> Keys
        {
            get { return _cache.Keys; }
        }

        /// <summary>
        /// Gets the Items
        /// </summary>
        public IEnumerable<TObject> Items
        {
            get { return _cache.Items; }
        }

        /// <summary>
        /// Gets the key value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<TKey,TObject>> KeyValues
        {
            get { return _cache.KeyValues; }
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
            return _cache.Lookup(key);
        }

        /// <summary>
        /// The total count of cached items
        /// </summary>
        public int Count
        {
            get { return _cache.Count; }
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _cache.Dispose();
        }

        #endregion

    }
}