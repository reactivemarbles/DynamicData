// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal
{
    internal class RefCount<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly object _locker = new object();
        private int _refCount;
        private IObservableList<T> _list;

        public RefCount(IObservable<IChangeSet<T>> source)
        {
            _source = source;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                lock (_locker)
                {
                    if (++_refCount == 1)
                    {
                        _list = _source.AsObservableList();
                    }
                }

                var subscriber = _list.Connect().SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();
                    IDisposable listToDispose = null;
                    lock (_locker)
                    {
                        if (--_refCount == 0)
                        {
                            listToDispose = _list;
                            _list = null;
                        }
                    }

                    listToDispose?.Dispose();
                });
            });
        }
    }
}
