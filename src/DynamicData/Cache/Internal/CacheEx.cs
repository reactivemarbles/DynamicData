// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the CacheEx class.
/// </summary>
internal static class CacheEx
{
    /// <summary>
    /// Executes the Clone operation.
    /// </summary>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="changes">The changes value.</param>
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

    /// <summary>
    /// Executes the GetInitialUpdates operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="filter">The filter value.</param>
    /// <returns>The result of the operation.</returns>
    public static IChangeSet<TObject, TKey> GetInitialUpdates<TObject, TKey>(this ChangeAwareCache<TObject, TKey> source, Func<TObject, bool>? filter = null)
        where TObject : notnull
        where TKey : notnull
    {
        var filtered = filter is null ? source.KeyValues : source.KeyValues.Where(kv => filter(kv.Value));
        return new ChangeSet<TObject, TKey>(filtered.Select(i => new Change<TObject, TKey>(ChangeReason.Add, i.Key, i.Value)));
    }
}
