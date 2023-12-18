// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

        return _source.Publish(
            shared =>
            {
                var locker = new object();
                var state = new Cache<TObject, TKey>();

                var inlineChange = shared.MergeMany(itemChangedTrigger).Synchronize(locker).Select(_ => new AnonymousQuery<TObject, TKey>(state));

                var sourceChanged = shared.Synchronize(locker).Scan(
                    state,
                    (cache, changes) =>
                    {
                        cache.Clone(changes);
                        return cache;
                    }).Select(list => new AnonymousQuery<TObject, TKey>(list));

                return sourceChanged.Merge(inlineChange);
            });
    }
}
