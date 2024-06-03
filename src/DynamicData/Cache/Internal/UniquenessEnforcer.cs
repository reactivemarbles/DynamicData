// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class UniquenessEnforcer<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() =>
/*
    For refresh,  we need to check whether there was a previous add or update in the batch. If not use refresh,
    otherwise use the previous update.
*/

        source
            .WhereReasonsAreNot(ChangeReason.Moved)
            .Scan(
                (ChangeAwareCache<TObject, TKey>?)null,
                (cache, changes) =>
                {
                    cache ??= new ChangeAwareCache<TObject, TKey>(changes.Count);

                    var grouped = changes.GroupBy(c => c.Key).Select(c =>
                    {
                        var all = c.ToArray();

                        if (all.Length > 1)
                        {
                            /* Extreme edge case where compound has mixture of changes ending in refresh */
                            // find the previous non-refresh and return if found
                            for (var i = all.Length - 1; i >= 0; i--)
                            {
                                var candidate = all[i];
                                if (candidate.Reason != ChangeReason.Refresh)
                                    return candidate;
                            }
                        }
                        // the entire batch are all refresh events
                        return all[0];
                    });

                    foreach (var change in grouped)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                cache.AddOrUpdate(change.Current, change.Key);
                                break;
                            case ChangeReason.Remove:
                                cache.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                cache.Refresh(change.Key);
                                break;
                        }
                    }

                    return cache;
                })
            .Select(state => state!.CaptureChanges());
}
