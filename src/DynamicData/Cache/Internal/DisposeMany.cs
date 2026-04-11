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
            var tracked = new KeyedDisposable<TKey>();

            var sourceSubscription = _source
                .SynchronizeSafe(locker, out var queue)
                .SubscribeSafe(Observer.Create<IChangeSet<TObject, TKey>>(
                    onNext: changeSet =>
                    {
                        observer.OnNext(changeSet);

                        foreach (var change in changeSet.ToConcreteType())
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add:
                                case ChangeReason.Update:
                                    tracked.AddIfDisposable(change.Key, change.Current);
                                    break;

                                case ChangeReason.Remove:
                                    tracked.Remove(change.Key);
                                    break;
                            }
                        }
                    },
                    onError: observer.OnError,
                    onCompleted: observer.OnCompleted));

            return Disposable.Create(() =>
            {
                sourceSubscription.Dispose();
                queue.EnsureDeliveryComplete();
                tracked.Dispose();
            });
        });
}