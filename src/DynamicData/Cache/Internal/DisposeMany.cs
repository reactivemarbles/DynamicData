// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class DisposeMany<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source;

    public IObservable<IChangeSet<TObject, TKey>> Run()
        => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var locker = InternalEx.NewLock();
            var cachedItems = new Dictionary<TKey, TObject>();

            var queue = new DeliveryQueue<DynamicData.Internal.Notification<IChangeSet<TObject, TKey>>>(locker, notification =>
            {
                if (notification.HasValue)
                {
                    var changeSet = notification.Value!;

                    observer.OnNext(changeSet);

                    foreach (var change in changeSet.ToConcreteType())
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Update:
                                if (change.Previous.HasValue && !EqualityComparer<TObject>.Default.Equals(change.Current, change.Previous.Value))
                                {
                                    (change.Previous.Value as IDisposable)?.Dispose();
                                }

                                break;

                            case ChangeReason.Remove:
                                (change.Current as IDisposable)?.Dispose();
                                break;
                        }
                    }

                    cachedItems.Clone(changeSet);
                }
                else if (notification.Error is not null)
                {
                    observer.OnError(notification.Error);
                    ProcessFinalization(cachedItems);
                }
                else
                {
                    observer.OnCompleted();
                    ProcessFinalization(cachedItems);
                }

                return !notification.IsTerminal;
            });

            var sourceSubscription = _source.SynchronizeSafe(queue);

            return Disposable.Create(() =>
            {
                sourceSubscription.Dispose();

                using (var readLock = queue.AcquireReadLock())
                {
                    ProcessFinalization(cachedItems);
                }
            });
        });

    private static void ProcessFinalization(Dictionary<TKey, TObject> cachedItems)
    {
        foreach (var pair in cachedItems)
        {
            (pair.Value as IDisposable)?.Dispose();
        }

        cachedItems.Clear();
    }
}
