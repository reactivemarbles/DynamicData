// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class FilterOnObservable<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
    {
        var cache = new ChangeAwareCache<TObject, TKey>();

        var changes = source.AggregateMany<TObject, TKey, (TObject Item, bool Passes), IChangeSet<TObject, TKey>>(
            onSource: (parentChanges, track) =>
            {
                foreach (var change in parentChanges.ToConcreteType())
                {
                    switch (change.Reason)
                    {
                        // Drop any cached value upfront. Synchronous emissions from the new inner observable
                        // collapse with this Remove inside the same captured changeset.
                        case ChangeReason.Add or ChangeReason.Update:
                            cache.Remove(change.Key);
                            var item = change.Current;
                            track(change.Key, filterFactory(item, change.Key).DistinctUntilChanged().Select(passes => (item, passes)));
                            break;

                        case ChangeReason.Remove:
                            cache.Remove(change.Key);
                            track(change.Key, null);
                            break;

                        case ChangeReason.Refresh:
                            cache.Refresh(change.Key);
                            break;
                    }
                }
            },
            onInner: (value, key) =>
            {
                if (value.Passes)
                {
                    cache.AddOrUpdate(value.Item, key);
                }
                else
                {
                    cache.Remove(key);
                }
            },
            emit: o =>
            {
                var captured = cache.CaptureChanges();
                if (captured.Count > 0)
                {
                    o.OnNext(captured);
                }
            });

        if (buffer is { } window)
        {
            changes = changes.Buffer(window, scheduler ?? GlobalConfig.DefaultScheduler)
                             .Where(static batches => batches.Count > 0)
                             .Select(static batches => new ChangeSet<TObject, TKey>(batches.SelectMany(static cs => cs)));
        }

        return changes.SubscribeSafe(observer);
    });
}
