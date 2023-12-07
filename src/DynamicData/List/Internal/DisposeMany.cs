// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class DisposeMany<T>(IObservable<IChangeSet<T>> source)
    where T : notnull
{
    public IObservable<IChangeSet<T>> Run()
        => Observable.Create<IChangeSet<T>>(observer =>
        {
            // Will be locking on cachedItems directly, instead of using an anonymous gate object. This is acceptable, since it's a privately-held object, there's no risk of deadlock from other consumers locking on it.
            var cachedItems = new List<T>();

            var sourceSubscription = source
                .Synchronize(cachedItems)
                .SubscribeSafe(Observer.Create<IChangeSet<T>>(
                    onNext: changeSet =>
                    {
                        observer.OnNext(changeSet);

                        foreach (var change in changeSet)
                        {
                            switch (change.Reason)
                            {
                                case ListChangeReason.Clear:
                                    foreach (var item in cachedItems)
                                    {
                                        (item as IDisposable)?.Dispose();
                                    }

                                    break;

                                case ListChangeReason.Remove:
                                    (change.Item.Current as IDisposable)?.Dispose();
                                    break;

                                case ListChangeReason.RemoveRange:
                                    foreach (var item in change.Range)
                                    {
                                        (item as IDisposable)?.Dispose();
                                    }

                                    break;

                                case ListChangeReason.Replace:
                                    if (change.Item.Previous.HasValue)
                                    {
                                        (change.Item.Previous.Value as IDisposable)?.Dispose();
                                    }

                                    break;
                            }
                        }

                        cachedItems.Clone(changeSet);
                    },
                    onError: error =>
                    {
                        observer.OnError(error);

                        ProcessFinalization(cachedItems);
                    },
                    onCompleted: () =>
                    {
                        observer.OnCompleted();

                        ProcessFinalization(cachedItems);
                    }));

            return Disposable.Create(() =>
            {
                sourceSubscription.Dispose();

                lock (cachedItems)
                {
                    ProcessFinalization(cachedItems);
                }
            });
        });

    private static void ProcessFinalization(List<T> cachedItems)
    {
        foreach (var item in cachedItems)
        {
            (item as IDisposable)?.Dispose();
        }

        cachedItems.Clear();
    }
}
