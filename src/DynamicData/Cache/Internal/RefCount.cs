// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class RefCount<TObject, TKey>
        where TKey : notnull
    {
        private readonly object _locker = new object();

        private readonly IObservable<IChangeSet<TObject, TKey>> _source;

        private IObservableCache<TObject, TKey>? _cache;

        private int _refCount;

        public RefCount(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(
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
    }
}