// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class DisposeMany<T>
    where T : notnull
{
    private readonly IObservable<IChangeSet<T>> _source;

    public DisposeMany(IObservable<IChangeSet<T>> source)
        => _source = source;

    public IObservable<IChangeSet<T>> Run()
        => Observable.Create<IChangeSet<T>>(observer =>
        {
            var cachedItems = new ChangeAwareList<T>();

            return _source.SubscribeSafe(Observer.Create<IChangeSet<T>>(
                onNext: changeSet =>
                {
                    observer.OnNext(changeSet);

                    foreach (var change in changeSet)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Clear:
                                foreach (var item in cachedItems)
                                    (item as IDisposable)?.Dispose();
                                break;

                            case ListChangeReason.Remove:
                                (change.Item.Current as IDisposable)?.Dispose();
                                break;

                            case ListChangeReason.RemoveRange:
                                foreach (var item in change.Range)
                                    (item as IDisposable)?.Dispose();
                                break;

                            case ListChangeReason.Replace:
                                if (change.Item.Previous.HasValue)
                                    (change.Item.Previous.Value as IDisposable)?.Dispose();
                                break;
                        }
                    }

                    cachedItems.Clone(changeSet);
                },
                onError: error =>
                {
                    observer.OnError(error);

                    foreach (var item in cachedItems)
                        (item as IDisposable)?.Dispose();

                    cachedItems.Clear();
                },
                onCompleted: () =>
                {
                    observer.OnCompleted();

                    foreach (var item in cachedItems)
                        (item as IDisposable)?.Dispose();

                    cachedItems.Clear();
                }));
        });
}
