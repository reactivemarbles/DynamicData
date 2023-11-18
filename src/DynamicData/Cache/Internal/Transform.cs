// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class Transform<TDestination, TSource, TKey>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>>? exceptionCallback = null, bool transformOnRefresh = false)
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Defer(RunImpl);

    private IObservable<IChangeSet<TDestination, TKey>> RunImpl() => source.Scan(
                (ChangeAwareCache<TDestination, TKey>?)null,
                (cache, changes) =>
                {
                    cache ??= new ChangeAwareCache<TDestination, TKey>(changes.Count);

                    foreach (var change in changes.ToConcreteType())
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                {
                                    TDestination transformed;
                                    if (exceptionCallback is not null)
                                    {
                                        try
                                        {
                                            transformed = transformFactory(change.Current, change.Previous, change.Key);
                                            cache.AddOrUpdate(transformed, change.Key);
                                        }
                                        catch (Exception ex)
                                        {
                                            exceptionCallback(new Error<TSource, TKey>(ex, change.Current, change.Key));
                                        }
                                    }
                                    else
                                    {
                                        transformed = transformFactory(change.Current, change.Previous, change.Key);
                                        cache.AddOrUpdate(transformed, change.Key);
                                    }
                                }

                                break;

                            case ChangeReason.Remove:
                                cache.Remove(change.Key);
                                break;

                            case ChangeReason.Refresh:
                                {
                                    if (transformOnRefresh)
                                    {
                                        var transformed = transformFactory(change.Current, change.Previous, change.Key);
                                        cache.AddOrUpdate(transformed, change.Key);
                                    }
                                    else
                                    {
                                        cache.Refresh(change.Key);
                                    }
                                }

                                break;

                            case ChangeReason.Moved:
                                // Do nothing !
                                break;
                        }
                    }

                    return cache;
                })
            .Where(x => x is not null)
            .Select(cache => cache!.CaptureChanges());
}
