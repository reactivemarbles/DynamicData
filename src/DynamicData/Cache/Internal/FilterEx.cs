// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal static class FilterEx
{
    public static void FilterChanges<TObject, TKey>(this ChangeAwareCache<TObject, TKey> cache, IChangeSet<TObject, TKey> changes, Func<TObject, bool> predicate)
        where TObject : notnull
        where TKey : notnull
    {
        foreach (var change in changes.ToConcreteType())
        {
            var key = change.Key;
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    {
                        var current = change.Current;
                        if (predicate(current))
                        {
                            cache.AddOrUpdate(current, key);
                        }
                    }

                    break;

                case ChangeReason.Update:
                    {
                        var current = change.Current;
                        if (predicate(current))
                        {
                            cache.AddOrUpdate(current, key);
                        }
                        else
                        {
                            cache.Remove(key);
                        }
                    }

                    break;

                case ChangeReason.Remove:
                    cache.Remove(key);
                    break;

                case ChangeReason.Refresh:
                    {
                        var existing = cache.Lookup(key);
                        if (predicate(change.Current))
                        {
                            if (!existing.HasValue)
                            {
                                cache.AddOrUpdate(change.Current, key);
                            }
                            else
                            {
                                cache.Refresh(key);
                            }
                        }
                        else if (existing.HasValue)
                        {
                            cache.Remove(key);
                        }
                    }

                    break;
            }
        }
    }

    public static IChangeSet<TObject, TKey> RefreshFilteredFrom<TObject, TKey>(this ChangeAwareCache<TObject, TKey> filtered, Cache<TObject, TKey> allData, Func<TObject, bool> predicate)
        where TObject : notnull
        where TKey : notnull
    {
        if (allData.Count == 0)
        {
            return ChangeSet<TObject, TKey>.Empty;
        }

        foreach (var kvp in allData.KeyValues)
        {
            var existing = filtered.Lookup(kvp.Key);
            var matches = predicate(kvp.Value);

            if (matches)
            {
                if (!existing.HasValue)
                {
                    filtered.Add(kvp.Value, kvp.Key);
                }
            }
            else if (existing.HasValue)
            {
                filtered.Remove(kvp.Key);
            }
        }

        return filtered.CaptureChanges();
    }
}
