// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TransformWithInlineUpdate class.
/// </summary>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="transformFactory">The transformFactory value.</param>
/// <param name="updateAction">The updateAction value.</param>
/// <param name="exceptionCallback">The exceptionCallback value.</param>
/// <param name="transformOnRefresh">The transformOnRefresh value.</param>
internal sealed class TransformWithInlineUpdate<TDestination, TSource, TKey>(IObservable<IChangeSet<TSource, TKey>> source,
                                 Func<TSource, TDestination> transformFactory,
                                 Action<TDestination, TSource> updateAction,
                                 Action<Error<TSource, TKey>>? exceptionCallback = null,
                                 bool transformOnRefresh = false)
    where TDestination : class
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
                                Transform(cache, change);
                                break;

                            case ChangeReason.Update:
                                InlineUpdate(cache, change);
                                break;

                            case ChangeReason.Remove:
                                cache.Remove(change.Key);
                                break;

                            case ChangeReason.Refresh:
                                if (transformOnRefresh)
                                {
                                    InlineUpdate(cache, change);
                                }
                                else
                                {
                                    cache.Refresh(change.Key);
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

    /// <summary>
    /// Executes the Transform operation.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="change">The change value.</param>
    private void Transform(ChangeAwareCache<TDestination, TKey> cache, in Change<TSource, TKey> change)
    {
        TDestination transformed;
        if (exceptionCallback is not null)
        {
            try
            {
                transformed = transformFactory(change.Current);
                cache.AddOrUpdate(transformed, change.Key);
            }
            catch (Exception ex)
            {
                exceptionCallback(new Error<TSource, TKey>(ex, change.Current, change.Key));
            }
        }
        else
        {
            transformed = transformFactory(change.Current);
            cache.AddOrUpdate(transformed, change.Key);
        }
    }

    /// <summary>
    /// Executes the InlineUpdate operation.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="change">The change value.</param>
    private void InlineUpdate(ChangeAwareCache<TDestination, TKey> cache, Change<TSource, TKey> change)
    {
        var previous = cache.Lookup(change.Key)
                                .ValueOrThrow(() => new MissingKeyException($"{change.Key} is not found."));
        if (exceptionCallback is not null)
        {
            try
            {
                updateAction(previous, change.Current);
            }
            catch (Exception ex)
            {
                exceptionCallback(new Error<TSource, TKey>(ex, change.Current, change.Key));
            }
        }
        else
        {
            updateAction(previous, change.Current);
        }

        cache.Refresh(change.Key);
    }
}
