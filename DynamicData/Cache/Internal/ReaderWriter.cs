using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class ReaderWriter<TObject, TKey> 
    {
        private readonly ChangeAwareCache<TObject, TKey> _cache = new ChangeAwareCache<TObject, TKey>();
        private readonly object _locker = new object();
        private readonly CacheUpdater<TObject, TKey> _updater;


        public ReaderWriter(Func<TObject, TKey> keySelector = null)
        {
            _updater = keySelector == null
                ? new CacheUpdater<TObject, TKey>(_cache)
                : new CacheUpdater<TObject, TKey>(_cache, new KeySelector<TObject, TKey>(keySelector));
        }

        #region Writers

        public IChangeSet<TObject, TKey> Write(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            IChangeSet<TObject, TKey> result;
            lock (_locker)
            {
                    _updater.Update(changes);
                    result = _updater.AsChangeSet();

            }
            return result;
        }

        public IChangeSet<TObject, TKey> Write(Action<ICacheUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            IChangeSet<TObject, TKey> result;
            lock (_locker)
            {
                updateAction(_updater);
                result = _updater.AsChangeSet();
            }
            return result;
        }

        public IChangeSet<TObject, TKey> Write(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            IChangeSet<TObject, TKey> result;
            lock (_locker)
            {
                updateAction(_updater);
                result = _updater.AsChangeSet();
            }
            return result;
        }

        #endregion

        #region Accessors

        public IEnumerable<TKey> Keys
        {
            get
            {
                IEnumerable<TKey> result;
                lock (_locker)
                    result = _cache.Keys.ToArray();

                return result;
            }
        }

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues
        {
            get
            {
                IEnumerable<KeyValuePair<TKey, TObject>> result;
                lock (_locker)
                    result = _cache.KeyValues.ToArray();

                return result;
            }
        }

        public IEnumerable<TObject> Items
        {
            get
            {
                IEnumerable<TObject> result;
                lock (_locker)
                    result = _cache.Items.ToArray();

                return result;
            }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            Optional<TObject> result;
            lock (_locker)
                result= _cache.Lookup(key);
   
            return result;
        }

        public int Count => _cache.Count;

        #endregion
    }
}
