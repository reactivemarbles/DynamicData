// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Cache.Internal
{
    internal static class CacheEx
    {

        public static IChangeSet<TObject, TKey> GetInitialUpdates<TObject, TKey>(this ChangeAwareCache<TObject, TKey> source, Func<TObject, bool> filter = null)
        {
            var filtered = filter == null ? source.KeyValues : source.KeyValues.Where(kv => filter(kv.Value));
            return new ChangeSet<TObject, TKey>(filtered.Select(i => new Change<TObject, TKey>(ChangeReason.Add, i.Key, i.Value)));
        }

        public static void Clone<TKey, TObject>(this IDictionary<TKey, TObject> source, IChangeSet<TObject, TKey> changes)
        {
            var enumerable = changes.ToConcreteType();
            foreach (var item in enumerable)
            {
                switch (item.Reason)
                {
                    case ChangeReason.Update:
                    case ChangeReason.Add:
                        source[item.Key] = item.Current;
                        break;
                    case ChangeReason.Remove:
                        source.Remove(item.Key);
                        break;
                }
            }
        }
    }
}
