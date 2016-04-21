using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Internal
{
    class RefCount<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;

        public RefCount(IObservable<IChangeSet<T>> source)
        {
            _source = source;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            int refCount = 0;
            var locker = new object();
            IObservableList<T> list = null;

            return Observable.Create<IChangeSet<T>>(observer =>
            {
                lock (locker)
                {
                    refCount++;
                    if (refCount == 1)

                        list = _source.AsObservableList();

                    // ReSharper disable once PossibleNullReferenceException (never the case!)
                    var subscriber = list.Connect().SubscribeSafe(observer);

                    return Disposable.Create(() =>
                    {
                        lock (locker)
                        {
                            refCount--;
                            subscriber.Dispose();
                            if (refCount != 0) return;
                            list.Dispose();
                            list = null;
                        }
                    });
                }
            });
        }
    }
}
