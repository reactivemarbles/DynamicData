// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class RefCount<T>(IObservable<IChangeSet<T>> source)
    where T : notnull
{
    private readonly object _locker = new();
    private IObservableList<T>? _list;

    private int _refCount;

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                lock (_locker)
                {
                    if (++_refCount == 1)
                    {
                        _list = source.AsObservableList();
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
