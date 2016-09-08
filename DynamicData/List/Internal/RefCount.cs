using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace DynamicData.Internal
{
    class RefCount<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private int _refCount = 0;
        IObservableList<T> _list = null;

        public RefCount(IObservable<IChangeSet<T>> source)
        {
            _source = source;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                Interlocked.Increment(ref _refCount);
                if (Volatile.Read(ref _refCount) == 1)
                {
                    Interlocked.Exchange(ref _list, _source.AsObservableList());
                }

                // ReSharper disable once PossibleNullReferenceException (never the case!)
                var subscriber = _list.Connect().SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    Interlocked.Decrement(ref _refCount);
                    subscriber.Dispose();
                    if (Volatile.Read(ref _refCount) != 0) return;
                    _list.Dispose();
                    Interlocked.Exchange(ref _list, null);
                });
            });
        }
    }
}
