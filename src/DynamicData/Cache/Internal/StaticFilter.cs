// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class StaticFilter<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, bool suppressEmptyChangeSets)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
                                                                {
                                                                    ChangeAwareCache<TObject, TKey>? cache = null;

                                                                    return source.Subscribe(
                                                                        changes =>
                                                                    {
                                                                        cache ??= new ChangeAwareCache<TObject, TKey>(changes.Count);

                                                                        cache.FilterChanges(changes, filter);
                                                                        var filtered = cache.CaptureChanges();

                                                                        if (filtered.Count != 0 || !suppressEmptyChangeSets)
                                                                        {
                                                                            observer.OnNext(filtered);
                                                                        }
                                                                    },
                                                                        observer.OnError,
                                                                        observer.OnCompleted);
                                                                });
}
