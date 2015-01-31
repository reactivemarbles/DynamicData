using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Cache designed to be used for custom operator construction.
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
            if (source == null) throw new ArgumentNullException("source");
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



        public void BatchUpdate(Action<IIntermediateUpdater<TObject, TKey>> updateAction)
        {
            _innnerCache.UpdateFromIntermediate(updateAction);
        }

        public IObservable<int> CountChanged
        {
            get { return _innnerCache.CountChanged; }
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter)
        {
            return _innnerCache.Connect(filter);
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return _innnerCache.Connect();
        }

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _innnerCache.Watch(key);
        }

        public int Count
        {
            get { return _innnerCache.Count; }
        }

        public IEnumerable<TObject> Items
        {
            get { return _innnerCache.Items; }
        }

        public IEnumerable<KeyValuePair<TKey,TObject>> KeyValues
        {
            get { return _innnerCache.KeyValues; }
        }

        public IEnumerable<TKey> Keys
        {
            get { return _innnerCache.Keys; }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            return _innnerCache.Lookup(key);
        }

        internal IChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool> filter = null)
        {
            return _innnerCache.GetInitialUpdates(filter);
        }


        public void Dispose()
        {
            _innnerCache.Dispose();
        }

        #endregion
    }
}