// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class Switch<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IObservable<IChangeSet<TObject, TKey>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var locker = new object();

                var destination = new LockFreeObservableCache<TObject, TKey>();

                var populator = Observable.Switch(
                    _sources.Do(
                        _ =>
                        {
                            lock (locker)
                            {
                                destination.Clear();
                            }
                        })).Synchronize(locker).PopulateInto(destination);

                return new CompositeDisposable(destination, populator, destination.Connect().SubscribeSafe(observer));
            });
}
