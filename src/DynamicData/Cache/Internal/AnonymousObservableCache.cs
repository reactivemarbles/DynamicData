// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class AnonymousObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
        where TKey : notnull
    {
        private readonly IObservableCache<TObject, TKey> _cache;

        public AnonymousObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _cache = new ObservableCache<TObject, TKey>(source);
        }

        public AnonymousObservableCache(IObservableCache<TObject, TKey> cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public int Count => _cache.Count;

        public IObservable<int> CountChanged => _cache.CountChanged;

        public IEnumerable<TObject> Items => _cache.Items;

        public IEnumerable<TKey> Keys => _cache.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true)
          => _cache.Connect(predicate, suppressEmptyChangeSets);

        public void Dispose() => _cache.Dispose();

        public Optional<TObject> Lookup(TKey key) => _cache.Lookup(key);

        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => _cache.Preview(predicate);

        public IObservable<Change<TObject, TKey>> Watch(TKey key) => _cache.Watch(key);
    }
}