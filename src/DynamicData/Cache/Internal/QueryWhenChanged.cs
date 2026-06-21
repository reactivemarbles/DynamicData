// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the QueryWhenChanged class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TValue">The type of the TValue value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="itemChangedTrigger">The itemChangedTrigger value.</param>
internal sealed class QueryWhenChanged<TObject, TKey, TValue>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>>? itemChangedTrigger = null)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IQuery<TObject, TKey>> Run()
    {
        if (itemChangedTrigger is null)
        {
            return Observable.Defer(() =>
                {
                    return _source.Scan(
                        (Cache<TObject, TKey>?)null,
                        (cache, changes) =>
                        {
                            cache ??= new Cache<TObject, TKey>(changes.Count);

                            cache.Clone(changes);
                            return cache;
                        });
                })
                .Select(cache => new AnonymousQuery<TObject, TKey>(cache!));
        }

        return Observable.Create<IQuery<TObject, TKey>>(observer =>
        {
            var queue = new SharedDeliveryQueue();
            var state = new Cache<TObject, TKey>();

            var shared = _source.Publish();

            var inlineChange = shared.MergeMany(itemChangedTrigger).SynchronizeSafe(queue).Select(_ => new AnonymousQuery<TObject, TKey>(state));

            var sourceChanged = shared.SynchronizeSafe(queue).Scan(
                state,
                (cache, changes) =>
                {
                    cache.Clone(changes);
                    return cache;
                }).Select(list => new AnonymousQuery<TObject, TKey>(list));

            return new CompositeDisposable(sourceChanged.Merge(inlineChange).SubscribeSafe(observer), shared.Connect(), queue);
        });
    }
}
