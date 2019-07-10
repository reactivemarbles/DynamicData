// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class Cache<TObject, TKey> : ICache<TObject, TKey>
    {
        private readonly Dictionary<TKey, TObject> _data;

        public int Count => _data.Count;
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;
        public IEnumerable<TObject> Items => _data.Values;
        public IEnumerable<TKey> Keys => _data.Keys;

        public static readonly Cache<TObject, TKey> Empty = new Cache<TObject, TKey>();

        public Cache(int capacity = -1)
        {
            _data = capacity > 1 ? new Dictionary<TKey, TObject>(capacity) : new Dictionary<TKey, TObject>();
        }

        public Cache(Dictionary<TKey, TObject> data)
        {
            _data = data;
        }

        public Cache<TObject, TKey> Clone()
        {
            return _data== null
                ? new Cache<TObject, TKey>()
                : new Cache<TObject, TKey>(new Dictionary<TKey, TObject>(_data));
        }

        public void Clone(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            foreach (var item in changes)
            {
                switch (item.Reason)
                {
                    case ChangeReason.Update:
                    case ChangeReason.Add:
                        {
                            _data[item.Key] = item.Current;
                        }

                        break;
                    case ChangeReason.Remove:
                        _data.Remove(item.Key);
                        break;
                }
            }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            return _data.Lookup(key);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            _data[key] = item;
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            if (keys is IList<TKey> list)
            {
                var enumerable = EnumerableIList.Create(list);
                foreach (var item in enumerable)
                {
                    Remove(item);
                }
            }
            else
            {
                foreach (var key in keys)
                {
                    Remove(key);
                }
            }
        }

        public void Remove(TKey key)
        {
            if (_data.ContainsKey(key))
            {
                _data.Remove(key);
            }
        }

        public void Clear()
        {
            _data.Clear();
        }

        /// <summary>
        /// Sends a signal for operators to recalculate it's state 
        /// </summary>
        public void Refresh()
        {

        }

        /// <summary>
        /// Refreshes the items matching the specified keys
        /// </summary>
        /// <param name="keys">The keys.</param>
        public void Refresh(IEnumerable<TKey> keys)
        {

        }

        /// <summary>
        /// Refreshes the item matching the specified key
        /// </summary>
        public void Refresh(TKey key)
        {

        }
    }
}
