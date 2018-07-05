using System;
using System.Linq;

namespace DynamicData.Cache.Internal
{
    internal static class CacheEx
    {
        public static IChangeSet<TObject, TKey> GetInitialUpdates<TObject, TKey>(this ReaderWriter<TObject, TKey> source, Func<TObject, bool> filter = null)
        {
            var filtered = filter == null ? source.KeyValues : source.KeyValues.Where(kv => filter(kv.Value));
            return new ChangeSet<TObject, TKey>(filtered.Select(i => new Change<TObject, TKey>(ChangeReason.Add, i.Key, i.Value)));
        }

        public static IChangeSet<TObject, TKey> GetInitialUpdates<TObject, TKey>(this ChangeAwareCache<TObject, TKey> source, Func<TObject, bool> filter = null)
        {
            var filtered = filter == null ? source.KeyValues : source.KeyValues.Where(kv => filter(kv.Value));
            return new ChangeSet<TObject, TKey>(filtered.Select(i => new Change<TObject, TKey>(ChangeReason.Add, i.Key, i.Value)));
        }
    }
}
