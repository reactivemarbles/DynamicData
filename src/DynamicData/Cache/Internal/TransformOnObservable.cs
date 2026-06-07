// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, bool transformOnRefresh = false)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Defer(() =>
    {
        var cache = new ChangeAwareCache<TDestination, TKey>();

        return source.OrchestrateMany<TSource, TKey, TDestination, IChangeSet<TDestination, TKey>>(
            onSourceChangeSet: (changes, track) =>
            {
                foreach (var change in changes.ToConcreteType())
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add or ChangeReason.Update:
                            track(change.Key, transform(change.Current, change.Key).DistinctUntilChanged());
                            break;

                        case ChangeReason.Remove:
                            cache.Remove(change.Key);
                            track(change.Key, null);
                            break;

                        case ChangeReason.Refresh:
                            if (transformOnRefresh)
                            {
                                track(change.Key, transform(change.Current, change.Key).DistinctUntilChanged());
                            }
                            else
                            {
                                // Let the downstream decide what this means.
                                cache.Refresh(change.Key);
                            }

                            break;
                    }
                }
            },
            onInner: (value, key) => cache.AddOrUpdate(value, key),
            onDrainComplete: observer =>
            {
                while (true)
                {
                    var captured = cache.CaptureChanges();
                    if (captured.Count == 0)
                    {
                        break;
                    }

                    observer.OnNext(captured);
                }
            });
    });
}
