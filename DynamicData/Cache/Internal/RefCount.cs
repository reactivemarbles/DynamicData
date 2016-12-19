using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Cache.Internal
{
    internal class RefCount<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly object _locker = new object();
        private int _refCount = 0;
        private IObservableCache<TObject, TKey> _cache = null;

        public RefCount([NotNull] IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                lock (_locker)
                    if (++_refCount == 1)
                        _cache = _source.AsObservableCache();

                var subscriber = _cache.Connect().SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();
                    IDisposable cacheToDispose = null;
                    lock (_locker)
                        if (--_refCount == 0)
                        {
                            cacheToDispose = _cache;
                            _cache = null;
                        }
                    if (cacheToDispose != null)
                        cacheToDispose.Dispose();
                });
            });
        }
    }
}
