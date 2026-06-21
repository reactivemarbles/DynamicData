// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the Transform class.
/// </summary>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="transformFactory">The transformFactory value.</param>
/// <param name="exceptionCallback">The exceptionCallback value.</param>
/// <param name="transformOnRefresh">The transformOnRefresh value.</param>
internal sealed class Transform<TDestination, TSource, TKey>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>>? exceptionCallback = null, bool transformOnRefresh = false)
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Defer(RunImpl);

    /// <summary>
    /// Executes the RunImpl operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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
