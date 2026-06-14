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
            onSourceChangeSet: (changes, context) =>
            {
                foreach (var change in changes.ToConcreteType())
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add or ChangeReason.Update:
                            context.Track(change.Key, transform(change.Current, change.Key).DistinctUntilChanged());
                            break;

                        case ChangeReason.Remove:
                            cache.Remove(change.Key);
                            context.Untrack(change.Key);
                            break;

                        case ChangeReason.Refresh:
                            if (transformOnRefresh)
                            {
                                context.Track(change.Key, transform(change.Current, change.Key).DistinctUntilChanged());
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
                var captured = cache.CaptureChanges();
                if (captured.Count != 0)
                {
                    observer.OnNext(captured);
                }
            });
    });
}
