// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class SizeLimiter<TObject, TKey>
        where TKey : notnull
    {
        private readonly ChangeAwareCache<ExpirableItem<TObject, TKey>, TKey> _cache = new();

        private readonly int _sizeLimit;

        public SizeLimiter(int size)
        {
            _sizeLimit = size;
        }

        public IChangeSet<TObject, TKey> Change(IChangeSet<ExpirableItem<TObject, TKey>, TKey> updates)
        {
            _cache.Clone(updates);

            var itemsToExpire = _cache.KeyValues.OrderByDescending(exp => exp.Value.ExpireAt).Skip(_sizeLimit).Select(exp => new Change<TObject, TKey>(ChangeReason.Remove, exp.Key, exp.Value.Value)).ToList();

            if (itemsToExpire.Count > 0)
            {
                _cache.Remove(itemsToExpire.Select(exp => exp.Key));
            }

            var notifications = _cache.CaptureChanges();
            var changed = notifications.Select(update => new Change<TObject, TKey>(update.Reason, update.Key, update.Current.Value, update.Previous.HasValue ? update.Previous.Value.Value : Optional<TObject>.None));

            return new ChangeSet<TObject, TKey>(changed);
        }

        public KeyValuePair<TKey, TObject>[] CloneAndReturnExpiredOnly(IChangeSet<ExpirableItem<TObject, TKey>, TKey> updates)
        {
            _cache.Clone(updates);
            _cache.CaptureChanges(); // Clear any changes

            return _cache.KeyValues.OrderByDescending(exp => exp.Value.Index).Skip(_sizeLimit).Select(kvp => new KeyValuePair<TKey, TObject>(kvp.Key, kvp.Value.Value)).ToArray();
        }
    }
}