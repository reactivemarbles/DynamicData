// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class ReaderWriter<TObject, TKey>(Func<TObject, TKey>? keySelector = null)
    where TObject : notnull
    where TKey : notnull
{
    private readonly object _locker = new();

    private CacheUpdater<TObject, TKey>? _activeUpdater;

    private Dictionary<TKey, TObject> _data = []; // could do with priming this on first time load

    public int Count
    {
        get
        {
            lock (_locker)
            {
                return _data.Count;
            }
        }
    }

    public TObject[] Items
    {
        get
        {
            lock (_locker)
            {
                return [.. _data.Values];
            }
        }
    }

    public TKey[] Keys
    {
        get
        {
            lock (_locker)
            {
                return [.. _data.Keys];
            }
        }
    }

    public IReadOnlyDictionary<TKey, TObject> KeyValues
    {
        get
        {
            lock (_locker)
            {
                return new Dictionary<TKey, TObject>(_data);
            }
        }
    }

    public ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool>? filter = null)
    {
        lock (_locker)
        {
            var dictionary = _data;

            if (dictionary.Count == 0)
            {
                return [];
            }

            var changes = filter is null ? new ChangeSet<TObject, TKey>(dictionary.Count) : [];

            foreach (var kvp in dictionary)
            {
                if (filter is null || filter(kvp.Value))
                {
                    changes.Add(new Change<TObject, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                }
            }

            return changes;
        }
    }

    public Optional<TObject> Lookup(TKey key)
    {
        lock (_locker)
        {
            return _data.Lookup(key);
        }
    }

    public ChangeSet<TObject, TKey> Write(IChangeSet<TObject, TKey> changes, Action<ChangeSet<TObject, TKey>>? previewHandler, bool collectChanges)
    {
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        return DoUpdate(updater => updater.Clone(changes), previewHandler, collectChanges);
    }

    public ChangeSet<TObject, TKey> Write(Action<ICacheUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>>? previewHandler, bool collectChanges)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return DoUpdate(updateAction, previewHandler, collectChanges);
    }

    public ChangeSet<TObject, TKey> Write(Action<ISourceUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>>? previewHandler, bool collectChanges)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        return DoUpdate(updateAction, previewHandler, collectChanges);
    }

    public void WriteNested(Action<ISourceUpdater<TObject, TKey>> updateAction)
    {
        lock (_locker)
        {
            if (_activeUpdater is null)
            {
                throw new InvalidOperationException("WriteNested can only be used if another write is already in progress.");
            }

            updateAction(_activeUpdater);
        }
    }

    private ChangeSet<TObject, TKey> DoUpdate(Action<CacheUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>>? previewHandler, bool collectChanges)
    {
        lock (_locker)
        {
            if (previewHandler is not null)
            {
                var copy = new Dictionary<TKey, TObject>(_data);
                var changeAwareCache = new ChangeAwareCache<TObject, TKey>(_data);

                _activeUpdater = new CacheUpdater<TObject, TKey>(changeAwareCache, keySelector);
                updateAction(_activeUpdater);
                _activeUpdater = null;

                var changes = changeAwareCache.CaptureChanges();

                InternalEx.Swap(ref copy, ref _data);
                previewHandler(changes);
                InternalEx.Swap(ref copy, ref _data);

                return changes;
            }

            if (collectChanges)
            {
                var changeAwareCache = new ChangeAwareCache<TObject, TKey>(_data);

                _activeUpdater = new CacheUpdater<TObject, TKey>(changeAwareCache, keySelector);
                updateAction(_activeUpdater);
                _activeUpdater = null;

                return changeAwareCache.CaptureChanges();
            }

            _activeUpdater = new CacheUpdater<TObject, TKey>(_data, keySelector);
            updateAction(_activeUpdater);
            _activeUpdater = null;

            return [];
        }
    }
}
