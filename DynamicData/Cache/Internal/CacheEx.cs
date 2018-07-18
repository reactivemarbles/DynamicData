using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal static class CacheEx
    {

        public static IChangeSet<TObject, TKey> GetInitialUpdates<TObject, TKey>(this ChangeAwareCache<TObject, TKey> source, Func<TObject, bool> filter = null)
        {
            var filtered = filter == null ? source.KeyValues : source.KeyValues.Where(kv => filter(kv.Value));
            return new ChangeSet<TObject, TKey>(filtered.Select(i => new Change<TObject, TKey>(ChangeReason.Add, i.Key, i.Value)));
        }

        public static ImmutableList<TObject> Clone<TKey, TObject>(this ImmutableList<TObject> souce, IChangeSet<TObject, TKey> changes)
        {
            var enumerator = changes.ToFastEnumerable();
            var result = souce;
            foreach (var change in enumerator)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        result = result.Add(change.Current);
                        break;
                    case ChangeReason.Update:
                        result = result.Remove(change.Previous.Value);
                        result = result.Add(change.Current);
                        break;
                    case ChangeReason.Remove:
                        result = result.Remove(change.Previous.Value);
                        break;
                }
            }
            return result;
        }


        public static void Clone<TKey, TObject>(this IDictionary<TKey, TObject> souce, IChangeSet<TObject, TKey> changes)
        {
            var enumerable = changes.ToFastEnumerable();
            foreach (var item in enumerable)
            {
                switch (item.Reason)
                {
                    case ChangeReason.Update:
                    case ChangeReason.Add:
                        souce[item.Key] = item.Current;
                        break;
                    case ChangeReason.Remove:
                        souce.Remove(item.Key);
                        break;
                }
            }
        }


    }
}
