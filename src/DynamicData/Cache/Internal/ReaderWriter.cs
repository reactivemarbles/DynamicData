// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ReaderWriter class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="keySelector">The keySelector value.</param>
internal sealed class ReaderWriter<TObject, TKey>(Func<TObject, TKey>? keySelector = null)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _locker field.
    /// </summary>
    private readonly Lock _locker = new();

    /// <summary>
    /// The _activeUpdater field.
    /// </summary>
    private CacheUpdater<TObject, TKey>? _activeUpdater;

    /// <summary>
    /// The _data field.
    /// </summary>
    private Dictionary<TKey, TObject> _data = []; // could do with priming this on first time load

    /// <summary>
    /// Gets the Count value.
    /// </summary>
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

    /// <summary>
    /// Gets the Items value.
    /// </summary>
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

    /// <summary>
    /// Gets the Keys value.
    /// </summary>
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

    /// <summary>
    /// Gets the KeyValues value.
    /// </summary>
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

    /// <summary>
    /// Executes the GetInitialUpdates operation.
    /// </summary>
    /// <param name="filter">The filter value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Executes the Lookup operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key)
    {
        lock (_locker)
        {
            return _data.Lookup(key);
        }
    }

    /// <summary>
    /// Executes the Write operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    /// <param name="previewHandler">The previewHandler value.</param>
    /// <param name="collectChanges">The collectChanges value.</param>
    /// <returns>The result of the operation.</returns>
    public ChangeSet<TObject, TKey> Write(IChangeSet<TObject, TKey> changes, Action<ChangeSet<TObject, TKey>>? previewHandler, bool collectChanges)
    {
        ArgumentExceptionHelper.ThrowIfNull(changes);

        return DoUpdate(updater => updater.Clone(changes), previewHandler, collectChanges);
    }

    /// <summary>
    /// Executes the Write operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    /// <param name="previewHandler">The previewHandler value.</param>
    /// <param name="collectChanges">The collectChanges value.</param>
    /// <returns>The result of the operation.</returns>
    public ChangeSet<TObject, TKey> Write(Action<ICacheUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>>? previewHandler, bool collectChanges)
    {
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

        return DoUpdate(updateAction, previewHandler, collectChanges);
    }

    /// <summary>
    /// Executes the Write operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    /// <param name="previewHandler">The previewHandler value.</param>
    /// <param name="collectChanges">The collectChanges value.</param>
    /// <returns>The result of the operation.</returns>
    public ChangeSet<TObject, TKey> Write(Action<ISourceUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>>? previewHandler, bool collectChanges)
    {
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

        return DoUpdate(updateAction, previewHandler, collectChanges);
    }

    /// <summary>
    /// Executes the WriteNested operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
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

    /// <summary>
    /// Executes the DoUpdate operation.
    /// </summary>
    /// <param name="updateAction">The updateAction value.</param>
    /// <param name="previewHandler">The previewHandler value.</param>
    /// <param name="collectChanges">The collectChanges value.</param>
    /// <returns>The result of the operation.</returns>
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
