// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal class RefCount<T>
{
    private readonly object _locker = new();

    private readonly IObservable<IChangeSet<T>> _source;

    private IObservableList<T>? _list;

    private int _refCount;

    public RefCount(IObservable<IChangeSet<T>> source)
    {
        _source = source;
    }

    public IObservable<IChangeSet<T>> Run()
    {
        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                lock (_locker)
                {
                    if (++_refCount == 1)
                    {
                        _list = _source.AsObservableList();
                    }
                }

                if (_list is null)
                {
                    throw new InvalidOperationException("The list is null despite having reference counting.");
                }

                var subscriber = _list.Connect().SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();
                        IDisposable? listToDispose = null;
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
