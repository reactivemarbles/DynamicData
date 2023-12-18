// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class StatusMonitor<T>(IObservable<T> source)
{
    public IObservable<ConnectionStatus> Run() => Observable.Create<ConnectionStatus>(
            observer =>
            {
                var statusSubject = new Subject<ConnectionStatus>();
                var status = ConnectionStatus.Pending;

                void Error(Exception ex)
                {
                    status = ConnectionStatus.Errored;
                    statusSubject.OnNext(status);
                    observer.OnError(ex);
                }

                void Completion()
                {
                    if (status == ConnectionStatus.Errored)
                    {
                        return;
                    }

                    status = ConnectionStatus.Completed;
                    statusSubject.OnNext(status);
                }

                void Updated()
                {
                    if (status != ConnectionStatus.Pending)
                    {
                        return;
                    }

                    status = ConnectionStatus.Loaded;
                    statusSubject.OnNext(status);
                }

                var monitor = source.Subscribe(_ => Updated(), Error, Completion);

                var subscriber = statusSubject.StartWith(status).DistinctUntilChanged().SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        statusSubject.OnCompleted();
                        monitor.Dispose();
                        subscriber.Dispose();
                    });
            });
}
