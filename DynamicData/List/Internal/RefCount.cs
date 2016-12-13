using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;

namespace DynamicData.Internal
{
    internal class RefCount<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private int _refCount = 0;
        private IObservableList<T> _list = null;

        public RefCount(IObservable<IChangeSet<T>> source)
        {
            _source = source;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                if (Interlocked.Increment(ref _refCount) == 1)
                {
                    Interlocked.Exchange(ref _list, _source.AsObservableList());
                }

                SpinWait.SpinUntil(() => _list != null);

                var subscriber = _list.Connect().SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();
                    var localList = _list;
                    if (Interlocked.Decrement(ref _refCount) == 0)
                    {
                        localList.Dispose();
                        Interlocked.CompareExchange(ref _list, null, localList);
                    }
                });
            });
        }
    }
}
