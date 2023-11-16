// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal static class CacheEx
{
    public static void Clone<TKey, TObject>(this IDictionary<TKey, TObject> source, IChangeSet<TObject, TKey> changes)
        where TKey : notnull
        where TObject : notnull
    {
        foreach (var item in changes.ToConcreteType())
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

    public static IChangeSet<TObject, TKey> GetInitialUpdates<TObject, TKey>(this ChangeAwareCache<TObject, TKey> source, Func<TObject, bool>? filter = null)
        where TObject : notnull
        where TKey : notnull
    {
        var filtered = filter is null ? source.KeyValues : source.KeyValues.Where(kv => filter(kv.Value));
        return new ChangeSet<TObject, TKey>(filtered.Select(i => new Change<TObject, TKey>(ChangeReason.Add, i.Key, i.Value)));
    }
}
