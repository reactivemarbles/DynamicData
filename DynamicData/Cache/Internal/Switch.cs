using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal sealed class Switch<TObject, TKey>
    {
        private readonly IObservable<IObservable<IChangeSet<TObject, TKey>>> _sources;

        public Switch(IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            _sources = sources;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var locker = new object();

                var destination = new LockFreeObservableCache<TObject, TKey>();

                var populator = Observable.Switch(_sources
                    .Do(_ =>
                    {
                        lock (locker)
                            destination.Clear();
                    }))
                    .Synchronize(locker)
                    .PopulateInto(destination);

                var publisher = destination.Connect().SubscribeSafe(observer);
                return new CompositeDisposable(destination, populator, publisher);
            });
        }
    }
}
