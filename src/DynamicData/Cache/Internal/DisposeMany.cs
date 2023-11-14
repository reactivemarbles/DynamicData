// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class DisposeMany<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public DisposeMany(IObservable<IChangeSet<TObject, TKey>> source)
        => _source = source;

    public IObservable<IChangeSet<TObject, TKey>> Run()
        => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var cachedItems = new Cache<TObject, TKey>();

            return _source.SubscribeSafe(Observer.Create<IChangeSet<TObject, TKey>>(
                onNext: changeSet =>
                {
                    observer.OnNext(changeSet);

                    foreach (var change in changeSet.ToConcreteType())
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Update:
                                if (change.Previous.HasValue && !EqualityComparer<TObject>.Default.Equals(change.Current, change.Previous.Value))
                                    (change.Previous.Value as IDisposable)?.Dispose();
                                break;

                            case ChangeReason.Remove:
                                (change.Current as IDisposable)?.Dispose();
                                break;
                        }
                    }

                    cachedItems.Clone(changeSet);
                },
                onError: error =>
                {
                    observer.OnError(error);

                    foreach (var item in cachedItems.Items)
                        (item as IDisposable)?.Dispose();

                    cachedItems.Clear();
                },
                onCompleted: () =>
                {
                    observer.OnCompleted();

                    foreach (var item in cachedItems.Items)
                        (item as IDisposable)?.Dispose();

                    cachedItems.Clear();
                }));
        });
}
