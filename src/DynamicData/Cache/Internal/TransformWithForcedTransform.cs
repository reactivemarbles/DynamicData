// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class TransformWithForcedTransform<TDestination, TSource, TKey>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>> forceTransform, Action<Error<TSource, TKey>>? exceptionCallback = null)
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Create<IChangeSet<TDestination, TKey>>(
            observer =>
            {
                var locker = new object();
                var shared = source.Synchronize(locker).Publish();

                // capture all items so we can apply a forced transform
                var cache = new Cache<TSource, TKey>();
                var cacheLoader = shared.Subscribe(changes => cache.Clone(changes));

                // create change set of items where force refresh is applied
                var refresher = forceTransform.Synchronize(locker).Select(selector => CaptureChanges(cache, selector)).Select(changes => new ChangeSet<TSource, TKey>(changes)).NotEmpty();

                var sourceAndRefreshes = shared.Merge(refresher);

                // do raw transform
                var transform = new Transform<TDestination, TSource, TKey>(sourceAndRefreshes, transformFactory, exceptionCallback, true).Run();

                return new CompositeDisposable(cacheLoader, transform.SubscribeSafe(observer), shared.Connect());
            });

    private static IEnumerable<Change<TSource, TKey>> CaptureChanges(Cache<TSource, TKey> cache, Func<TSource, TKey, bool> shouldTransform) =>
        cache.KeyValues.Where(kvp => shouldTransform(kvp.Value, kvp.Key)).Select(kvp => new Change<TSource, TKey>(ChangeReason.Refresh, kvp.Key, kvp.Value));
}
