// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TransformWithForcedTransform class.
/// </summary>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="transformFactory">The transformFactory value.</param>
/// <param name="forceTransform">The forceTransform value.</param>
/// <param name="exceptionCallback">The exceptionCallback value.</param>
internal sealed class TransformWithForcedTransform<TDestination, TSource, TKey>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, ReactiveUI.Primitives.Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>> forceTransform, Action<Error<TSource, TKey>>? exceptionCallback = null)
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Create<IChangeSet<TDestination, TKey>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();
                var shared = source.SynchronizeSafe(queue).Publish();

                // capture all items so we can apply a forced transform
                var cache = new Cache<TSource, TKey>();
                var cacheLoader = shared.Subscribe(changes => cache.Clone(changes));

                // create change set of items where force refresh is applied
                var refresher = forceTransform.SynchronizeSafe(queue).Select(selector => CaptureChanges(cache, selector)).Select(changes => new ChangeSet<TSource, TKey>(changes)).NotEmpty();

                var sourceAndRefreshes = shared.Merge(refresher);

                // do raw transform
                var transform = new Transform<TDestination, TSource, TKey>(sourceAndRefreshes, transformFactory, exceptionCallback, true).Run();

                return new CompositeDisposable(cacheLoader, transform.SubscribeSafe(observer), shared.Connect(), queue);
            });

    /// <summary>
    /// Executes the CaptureChanges operation.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="shouldTransform">The shouldTransform value.</param>
    /// <returns>The result of the operation.</returns>
    private static IEnumerable<Change<TSource, TKey>> CaptureChanges(Cache<TSource, TKey> cache, Func<TSource, TKey, bool> shouldTransform) =>
        cache.KeyValues.Where(kvp => shouldTransform(kvp.Value, kvp.Key)).Select(kvp => new Change<TSource, TKey>(ChangeReason.Refresh, kvp.Key, kvp.Value));
}
