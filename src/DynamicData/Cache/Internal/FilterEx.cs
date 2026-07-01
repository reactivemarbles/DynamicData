// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the FilterEx class.
/// </summary>
internal static class FilterEx
{
    /// <summary>
    /// Executes the FilterChanges operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="cache">The cache value.</param>
    /// <param name="changes">The changes value.</param>
    /// <param name="predicate">The predicate value.</param>
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

    /// <summary>
    /// Executes the RefreshFilteredFrom operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="filtered">The filtered value.</param>
    /// <param name="allData">The allData value.</param>
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
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
