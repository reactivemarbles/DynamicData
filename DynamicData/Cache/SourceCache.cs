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
        public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromSource(updateAction);

        /// <inheritdoc />
        public IObservable<int> CountChanged => _innerCache.CountChanged;

        /// <inheritdoc />
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null) => _innerCache.Connect(predicate);
        /// <inheritdoc />
        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool> predicate = null) => _innerCache.Preview(predicate);

        /// <inheritdoc />
        public IObservable<Change<TObject, TKey>> Watch(TKey key) => _innerCache.Watch(key);

        /// <inheritdoc />
        public int Count => _innerCache.Count;

        /// <inheritdoc />
        public IEnumerable<TObject> Items => _innerCache.Items;

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _innerCache.KeyValues;

        /// <inheritdoc />
        public IEnumerable<TKey> Keys => _innerCache.Keys;

        /// <inheritdoc />
        public Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

        /// <inheritdoc />
        public void Dispose() => _innerCache.Dispose();

        #endregion
    }
}
