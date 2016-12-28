using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal
{
    internal class RefCount<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly object _locker = new object();
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
                lock (_locker)
                    if (++_refCount == 1)
                        _list = _source.AsObservableList();

                var subscriber = _list.Connect().SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();
                    IDisposable listToDispose = null;
                    lock (_locker)
                        if (--_refCount == 0)
                        {
                            listToDispose = _list;
                            _list = null;
                        }
                    if (listToDispose != null)
                        listToDispose.Dispose();
                });
            });
        }
    }
}
