// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An observable cache which exposes an update API.  Used at the root
    /// of all observable chains.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public sealed class SourceCache<TObject, TKey> : ISourceCache<TObject, TKey>
        where TKey : notnull
    {
        private readonly ObservableCache<TObject, TKey> _innerCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceCache{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="keySelector">The key selector.</param>
        /// <exception cref="System.ArgumentNullException">keySelector.</exception>
        public SourceCache(Func<TObject, TKey> keySelector)
        {
            KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            _innerCache = new ObservableCache<TObject, TKey>(keySelector);
        }

        /// <inheritdoc />
        public int Count => _innerCache.Count;

        /// <inheritdoc />
        public IObservable<int> CountChanged => _innerCache.CountChanged;

        /// <inheritdoc />
        public IEnumerable<TObject> Items => _innerCache.Items;

        /// <inheritdoc />
        public IEnumerable<TKey> Keys => _innerCache.Keys;

        /// <inheritdoc/>
        public Func<TObject, TKey> KeySelector { get; }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _innerCache.KeyValues;

        /// <inheritdoc />
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) => _innerCache.Connect(predicate, suppressEmptyChangeSets);

        /// <inheritdoc />
        public void Dispose() => _innerCache.Dispose();

        /// <inheritdoc />
        public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromSource(updateAction);

        /// <inheritdoc />
        public Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

        /// <inheritdoc />
        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => _innerCache.Preview(predicate);

        /// <inheritdoc />
        public IObservable<Change<TObject, TKey>> Watch(TKey key) => _innerCache.Watch(key);
    }
}