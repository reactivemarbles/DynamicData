using System;

namespace DynamicData.Cache.Internal
{
    internal static class FilterEx
    {
        public static IChangeSet<TObject, TKey> RefreshFilteredFrom<TObject, TKey>(
            this ChangeAwareCache<TObject, TKey> filtered, 
            Cache<TObject, TKey> allData,
            Func<TObject, bool> predicate)
        {
            foreach (var kvp in allData.KeyValues)
            {
                var exisiting = filtered.Lookup(kvp.Key);
                var matches = predicate(kvp.Value);

                if (matches)
                {
                    if (!exisiting.HasValue)
                        filtered.AddOrUpdate(kvp.Value, kvp.Key);
                }
                else
                {
                    if (exisiting.HasValue)
                        filtered.Remove(kvp.Key);
                }
            }
            return filtered.CaptureChanges();
        }
        
        public static void FilterChanges<TObject, TKey>(this ChangeAwareCache<TObject, TKey> cache,
            IChangeSet<TObject, TKey> changes,
            Func<TObject, bool> predicate)
        {
            foreach (var change in changes)
            {
                var key = change.Key;
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                    {
                        if (predicate(change.Current))
                            cache.AddOrUpdate(change.Current, key);
                    }
                        break;
                    case ChangeReason.Update:
                    {
                        if (predicate(change.Current))
                            cache.AddOrUpdate(change.Current, key);
                        else
                            cache.Remove(key);
                    }
                        break;
                    case ChangeReason.Remove:
                        cache.Remove(key);
                        break;
                    case ChangeReason.Refresh:
                    {
                        var exisiting = cache.Lookup(key);
                        if (predicate(change.Current))
                        {
                            if (!exisiting.HasValue)
                                cache.AddOrUpdate(change.Current, key);
                            else
                                cache.Refresh(key);
                        }
                        else
                        {
                            if (exisiting.HasValue)
                                cache.Remove(key);
                        }
                    }
                        break;
                }
            }
        }

    }
}