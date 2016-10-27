using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class StatusMonitor<T>
    {
        private readonly IObservable<T> _source;

        public StatusMonitor(IObservable<T> source)
        {
            _source = source;
        }

        public IObservable<ConnectionStatus> Run()
        {
            return Observable.Create<ConnectionStatus>(observer =>
            {
                var statusSubject = new Subject<ConnectionStatus>();
                var status = ConnectionStatus.Pending;

                Action<Exception> error = (ex) =>
                {
                    status = ConnectionStatus.Errored;
                    statusSubject.OnNext(status);
                    observer.OnError(ex);
                };

                Action completion = () =>
                {
                    if (status == ConnectionStatus.Errored) return;
                    status = ConnectionStatus.Completed;
                    statusSubject.OnNext(status);
                };

                Action updated = () =>
                {
                    if (status != ConnectionStatus.Pending) return;
                    status = ConnectionStatus.Loaded;
                    statusSubject.OnNext(status);
                };

                var monitor = _source.Subscribe(_ => updated(), error, completion);

                var subscriber = statusSubject
                    .StartWith(status)
                    .DistinctUntilChanged()
                    .SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    statusSubject.OnCompleted();
                    monitor.Dispose();
                    subscriber.Dispose();
                });
            });
        }
    }
}