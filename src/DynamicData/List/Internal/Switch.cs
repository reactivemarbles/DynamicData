// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class Switch<T>(IObservable<IObservable<IChangeSet<T>>> sources)
    where T : notnull
{
    private readonly IObservable<IObservable<IChangeSet<T>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = new object();

                var destination = new SourceList<T>();

                var populator = Observable.Switch(
                    _sources.Do(
                        _ =>
                        {
                            lock (locker)
                            {
                                destination.Clear();
                            }
                        })).Synchronize(locker).PopulateInto(destination);

                var publisher = destination.Connect().SubscribeSafe(observer);
                return new CompositeDisposable(destination, populator, publisher);
            });
}
