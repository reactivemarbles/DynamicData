// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal class UniquenessEnforcer<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() =>
        /*
* If we handle refreshes, we cannot use .Last() as the last in the groupd may be a refresh,
* and a previous in the group may add or update. Suddenly this scenario becomes very complicated
* so for this phase we'll ignore these.
*
*/

        source
            .WhereReasonsAreNot(ChangeReason.Refresh, ChangeReason.Moved)
            .Scan(
                new ChangeAwareCache<TObject, TKey>(),
                (cache, changes) =>
                {
                    var grouped = changes.GroupBy(c => c.Key).Select(c => c.Last());

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
                        }
                    }

                    return cache;
                })
            .Select(state => state.CaptureChanges());
}
