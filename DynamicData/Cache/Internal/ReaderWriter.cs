using System;
using System.Collections.Generic;
using System.Threading;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class ReaderWriter<TObject, TKey> : IDisposable
    {
        private readonly Func<TObject, TKey> _keySelector;
        private Dictionary<TKey, TObject> _data = new Dictionary<TKey, TObject>();
        private CacheUpdater<TObject, TKey> _activeUpdater = null;

        private TwoStageRWLock _lock = new TwoStageRWLock(LockRecursionPolicy.SupportsRecursion);

        public ReaderWriter(Func<TObject, TKey> keySelector = null)
        {
            _keySelector = keySelector;
        }

        #region Writers

        public ChangeSet<TObject, TKey> Write(IChangeSet<TObject, TKey> changes, bool collectChanges)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            return DoUpdate(updater => updater.Clone(changes), collectChanges);
        }

        public ChangeSet<TObject, TKey> Write(Action<ICacheUpdater<TObject, TKey>> updateAction, bool collectChanges)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            return DoUpdate(updateAction, collectChanges);
        }

        public ChangeSet<TObject, TKey> Write(Action<ISourceUpdater<TObject, TKey>> updateAction, bool collectChanges)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            return DoUpdate(updateAction, collectChanges);
        }

        private ChangeSet<TObject, TKey> DoUpdate(Action<CacheUpdater<TObject, TKey>> updateAction, bool collectChanges)
        {
            _lock.EnterWriteLock();
            try
            {
                if (collectChanges)
                {
                    var changeAwareCache = new ChangeAwareCache<TObject, TKey>(_data);

                    _activeUpdater = new CacheUpdater<TObject, TKey>(changeAwareCache, _keySelector);
                    updateAction(_activeUpdater);
                    _activeUpdater = null;

                    return changeAwareCache.CaptureChanges();
                }
                else
                {
                    _activeUpdater = new CacheUpdater<TObject, TKey>(_data, _keySelector);
                    updateAction(_activeUpdater);
                    _activeUpdater = null;

                    return ChangeSet<TObject, TKey>.Empty;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal void WriteNested(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_activeUpdater == null)
                {
                    throw new InvalidOperationException("WriteNested can only be used if another write is already in progress.");
                }
                updateAction(_activeUpdater);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        #endregion

        #region Accessors

        public ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool> filter = null)
        {
            ChangeSet<TObject, TKey> result;
            _lock.EnterReadLock();
            try
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
            finally
            {
                _lock.ExitReadLock();
            }
            return result;
        }

        public TKey[] Keys
        {
            get
            {
                TKey[] result;
                _lock.EnterReadLock();
                try
                {
                    result = new TKey[_data.Count];
                    _data.Keys.CopyTo(result, 0);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
                return result;
            }
        }

        public KeyValuePair<TKey, TObject>[] KeyValues
        {
            get
            {
                KeyValuePair<TKey, TObject>[] result;
                _lock.EnterReadLock();
                try
                {
                    result = new KeyValuePair<TKey, TObject>[_data.Count];
                    int i = 0;
                    foreach (var kvp in _data)
                    {
                        result[i] = kvp;
                        i++;
                    }
                }
                finally
                {
                    _lock.ExitReadLock();
                }
                return result;
            }
        }

        public TObject[] Items
        {
            get
            {
                TObject[] result;
                _lock.EnterReadLock();
                try
                {
                    result = new TObject[_data.Count];
                    _data.Values.CopyTo(result, 0);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
                return result;
            }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            Optional<TObject> result;
            _lock.EnterReadLock();
            try
            {
                result = _data.Lookup(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return result;
        }

        public int Count
        {
            get
            {
                int count;
                _lock.EnterReadLock();
                try
                {
                    count = _data.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }

                return count;
            }
        }

        #endregion

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
