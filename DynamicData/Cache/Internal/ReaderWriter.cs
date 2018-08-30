using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class ReaderWriter<TObject, TKey> 
    {
        private readonly Func<TObject, TKey> _keySelector;
        private readonly ChangeAwareCache<TObject, TKey> _changeAwareCache ;
        private readonly Dictionary<TKey,TObject> _data = new Dictionary<TKey, TObject>();
        
        private readonly object _locker = new object();

        public ReaderWriter(Func<TObject, TKey> keySelector = null)
        {
            _keySelector = keySelector;
            _changeAwareCache = new ChangeAwareCache<TObject, TKey>(_data);
        }

        #region Writers

        public ChangeSet<TObject, TKey> Write(IChangeSet<TObject, TKey> changes, bool notifyChanges)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            ChangeSet<TObject, TKey> result;
            lock (_locker)
            {
                
                if (notifyChanges)
                {
                    _changeAwareCache.Clone(changes);
                    result = _changeAwareCache.CaptureChanges();
                }
                else
                {
                    _data.Clone(changes);
                    result = ChangeSet<TObject, TKey>.Empty;
                }
            }
            return result;
        }

        public ChangeSet<TObject, TKey> Write(Action<ICacheUpdater<TObject, TKey>> updateAction, bool notifyChanges)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            ChangeSet<TObject, TKey> result;
            lock (_locker)
            {
                var updater = CreateUpdater(notifyChanges);
                updateAction(updater);
                result = _changeAwareCache.CaptureChanges();
            }
            return result;
        }


        public ChangeSet<TObject, TKey> Write(Action<ISourceUpdater<TObject, TKey>> updateAction, bool notifyChanges)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            ChangeSet<TObject, TKey> result;
            lock (_locker)
            {
                var updater = CreateUpdater(notifyChanges);
                updateAction(updater);
                result = _changeAwareCache.CaptureChanges();
            }
            return result;
        }
        
        private CacheUpdater<TObject, TKey> CreateUpdater(bool notifyChanges)
        {
            return notifyChanges 
                ? new CacheUpdater<TObject, TKey>(_changeAwareCache, _keySelector) 
                : new CacheUpdater<TObject, TKey>(_data, _keySelector);
        }

        #endregion

        #region Accessors
        
        public ChangeSet<TObject, TKey> GetInitialUpdates( Func<TObject, bool> filter = null)
        {
            ChangeSet<TObject, TKey> result;
            lock (this)
            {
                var dictionary = _data;

                if (dictionary.Count == 0)
                    return ChangeSet<TObject, TKey>.Empty;

                var changes = filter == null
                    ? new ChangeSet<TObject, TKey>(dictionary.Count)
                    : new ChangeSet<TObject, TKey>();

                foreach (var kvp in dictionary)
                {
                    if (filter == null || filter(kvp.Value))
                        changes.Add(new Change<TObject, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                }

                result = changes;
            }
            return result;
        }

        public TKey[] Keys
        {
            get
            {
                TKey[] result;
                lock (_locker)
                {
                    result = new TKey[_data.Count];
                    _data.Keys.CopyTo(result, 0);
                }
                return result;
            }
        }

        public KeyValuePair<TKey, TObject>[] KeyValues
        {
            get
            {
                KeyValuePair<TKey, TObject>[] result;
                lock (_locker)
                {
                    result = new KeyValuePair<TKey, TObject>[_data.Count];
                    int i = 0;
                    foreach (var kvp in _data)
                    {
                        result[i] = kvp;
                        i++;
                    }
                }
                return result;
            }
        }

        public TObject[] Items
        {
            get
            {
                TObject[] result;
                lock (_locker)
                {
                    result = new TObject[_data.Count];
                    _data.Values.CopyTo(result, 0);
                }
                return result;
            }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            Optional<TObject> result;
            lock (_locker)
                result= _data.Lookup(key);
   
            return result;
        }

        public int Count
        {
            get
            {
                int count;
                lock (_locker)
                    count = _data.Count;

                return count;
            }
        }

        #endregion
    }
}
