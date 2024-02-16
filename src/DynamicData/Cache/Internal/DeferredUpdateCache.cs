// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal class DeferredUpdateCache<TObject, TKey> : IObservableCache<TObject, TKey>, IDisposable
    where TObject : notnull
    where TKey : notnull
{
    private readonly ChangeAwareCache<TObject, TKey> _changeAwareCache = new();
    private readonly Subject<IChangeSet<TObject, TKey>> _changeSetSubject = new();
    private readonly Lazy<ISubject<int>> _countChanged = new(() => new Subject<int>());
    private bool _isDisposed;

    public int Count => _changeAwareCache.Count;

    public IEnumerable<TObject> Items => _changeAwareCache.Items;

    public IEnumerable<TKey> Keys => _changeAwareCache.Keys;

    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _changeAwareCache.KeyValues;

    public IObservable<int> CountChanged => Observable.Defer(() => _countChanged.Value.StartWith(Count).DistinctUntilChanged());

    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) => throw new NotImplementedException();

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _changeSetSubject.OnCompleted();
            _changeSetSubject.Dispose();
        }
    }

    public Optional<TObject> Lookup(TKey key) => throw new NotImplementedException();

    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => throw new NotImplementedException();

    public IObservable<Change<TObject, TKey>> Watch(TKey key) => throw new NotImplementedException();

    public IDeferredCacheUpdater<TObject, TKey> DeferUpdate() => new DeferredCacheUpdater(_changeAwareCache, Disposable.Create(EmitChanges));

    private void EmitChanges()
    {
        throw new NotImplementedException();
    }

    private class DeferredCacheUpdater(ChangeAwareCache<TObject, TKey> cache, IDisposable disposable) : IDeferredCacheUpdater<TObject, TKey>
    {
        private IDisposable? _disposable = disposable;

        public int Count => cache.Count;

        public IEnumerable<TObject> Items => cache.Items;

        public IEnumerable<TKey> Keys => cache.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => cache.KeyValues;

        public void Dispose()
        {
            (_disposable ?? throw new ObjectDisposedException(nameof(_disposable))).Dispose();
            _disposable = null;
        }

        public void AddOrUpdate(TObject item, TKey key) => cache.AddOrUpdate(item, key);

        public void AddOrUpdate(IEnumerable<TObject> items, Func<TObject, TKey> keySelector) => items.ForEach(item => cache.AddOrUpdate(item, keySelector(item)));

        public void Clear() => cache.Clear();

        public void Remove(TKey key) => cache.Remove(key);

        public void Remove(IEnumerable<TKey> keys) => cache.Remove(keys);

        public void Remove(IEnumerable<TObject> items, Func<TObject, TKey> keySelector) => items.ForEach(item => cache.Remove(keySelector(item)));

        public void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> keyValuePairs) => keyValuePairs.ForEach(AddOrUpdate);

        public void AddOrUpdate(KeyValuePair<TKey, TObject> item) => cache.AddOrUpdate(item.Value, item.Key);

        public void Clone(IChangeSet<TObject, TKey> changes) => cache.Clone(changes);

        [Obsolete(Constants.EvaluateIsDead)]
        public void Evaluate(IEnumerable<TKey> keys) => Refresh(keys);

        // [Obsolete(Constants.EvaluateIsDead)]
        // public void Evaluate(IEnumerable<TObject> items) => Refresh(items);

        // [Obsolete(Constants.EvaluateIsDead)]
        // public void Evaluate(TObject item) => Refresh(item);
        [Obsolete(Constants.EvaluateIsDead)]
        public void Evaluate() => Refresh();

        [Obsolete(Constants.EvaluateIsDead)]
        public void Evaluate(TKey key) => Refresh(key);

        public TKey GetKey(TObject item) => throw new NotImplementedException();

        public IEnumerable<KeyValuePair<TKey, TObject>> GetKeyValues(IEnumerable<TObject> items) => throw new NotImplementedException();

        public void Refresh() => cache.Refresh();

        public void Refresh(IEnumerable<TKey> keys) => cache.Refresh(keys);

        public void Refresh(TKey key) => cache.Refresh(key);

        public void Remove(IEnumerable<KeyValuePair<TKey, TObject>> items) => items.ForEach(kvp => cache.Remove(kvp.Key));

        public void Remove(KeyValuePair<TKey, TObject> item) => cache.Remove(item.Key);

        public void RemoveKey(TKey key) => cache.Remove(key);

        public void RemoveKeys(IEnumerable<TKey> key) => cache.Remove(key);

        public void Update(IChangeSet<TObject, TKey> changes) => throw new NotImplementedException();

        public Optional<TObject> Lookup(TKey key) => cache.Lookup(key);
    }
}
