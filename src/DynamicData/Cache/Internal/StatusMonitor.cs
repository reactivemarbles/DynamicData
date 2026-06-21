// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

internal sealed class StatusMonitor<T>(IObservable<T> source)
{
    public IObservable<ConnectionStatus> Run() => Observable.Create<ConnectionStatus>(
            observer =>
            {
                var statusSubject = new Signal<ConnectionStatus>();
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
