// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class FilterOnObservable<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Defer(() =>
    {
        var changes = source.OrchestrateManyChanges<TObject, TKey, bool, TObject>(
            innerFactory: (item, key) => filterFactory(item, key).DistinctUntilChanged(),
            onSourceChange: static (cache, change) =>
            {
                // Drop the entry upfront on Add/Update/Remove. Synchronous emissions from the new inner
                // observable collapse with this Remove inside the same drain cycle, so a passing item
                // immediately re-adds. Refresh is propagated as-is.
                if (change.Reason is ChangeReason.Add or ChangeReason.Update or ChangeReason.Remove)
                {
                    cache.Remove(change.Key);
                }
                else if (change.Reason is ChangeReason.Refresh)
                {
                    cache.Refresh(change.Key);
                }
            },
            onInner: static (cache, key, item, passes) =>
            {
                if (passes)
                {
                    cache.AddOrUpdate(item, key);
                }
                else
                {
                    cache.Remove(key);
                }
            });

        if (buffer is { } window)
        {
            var sched = scheduler ?? GlobalConfig.DefaultScheduler;
            var quiet = TimeSpan.FromTicks(window.Ticks / 2);

            // Quiescence-based buffering with a hard latency cap:
            //   - Throttle(window/2): close the buffer after window/2 of source quiet (let bursts settle)
            //   - Timer(window):       cap at the full window so sustained streams cannot starve the boundary
            //   - Amb picks whichever fires first
            // Single-changeset windows are forwarded as-is to avoid an extra ChangeSet allocation.
            changes = changes.Publish(published => published.Buffer(() =>
                                           published.Throttle(quiet, sched).Select(static _ => Unit.Default)
                                                    .Amb(Observable.Timer(window, sched).Select(static _ => Unit.Default))))
                             .Where(static batches => batches.Count > 0)
                             .Select(static batches => batches.Count == 1
                                 ? batches[0]
                                 : new ChangeSet<TObject, TKey>(batches.SelectMany(static cs => cs)));
        }

        return changes;
    });
}
