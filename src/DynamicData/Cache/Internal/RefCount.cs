// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class RefCount<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    private readonly object _locker = new();

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    private IObservableCache<TObject, TKey>? _cache;

    private int _refCount;

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                lock (_locker)
                {
                    if (++_refCount == 1)
                    {
                        _cache = _source.AsObservableCache();
                    }
                }

                if (_cache is null)
                {
                    throw new InvalidOperationException(nameof(_cache) + " is null");
                }

                var subscriber = _cache.Connect().SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();
                    IDisposable? cacheToDispose = null;
                    lock (_locker)
                    {
                        if (--_refCount == 0)
                        {
                            cacheToDispose = _cache;
                            _cache = null;
                        }
                    }

                    cacheToDispose?.Dispose();
                });
            });
}
