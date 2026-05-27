// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class QueryWhenChanged<TObject, TKey, TValue>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>>? itemChangedTrigger = null)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

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
            var state = new Cache<TObject, TKey>();

            var shared = _source.Publish();

            var merged = DeliveryQueueMergeExtensions.DeliveryQueueMerge<IChangeSet<TObject, TKey>, TValue, IQuery<TObject, TKey>>(
                shared,
                changes =>
                {
                    state.Clone(changes);
                    return new AnonymousQuery<TObject, TKey>(state);
                },
                shared.MergeMany(itemChangedTrigger),
                _ => new AnonymousQuery<TObject, TKey>(state));

            return new CompositeDisposable(merged.SubscribeSafe(observer), shared.Connect());
        });
    }
}
