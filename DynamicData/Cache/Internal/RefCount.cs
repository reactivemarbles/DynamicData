using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Annotations;

namespace DynamicData.Cache.Internal
{
    internal class RefCount<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
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
                if (Interlocked.Increment(ref _refCount) == 1)
                {
                    Interlocked.Exchange(ref _cache, _source.AsObservableCache());
                }

                SpinWait.SpinUntil(() => _cache != null);

                var subscriber = _cache.Connect().SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();
                    var localCache = _cache;
                    if (Interlocked.Decrement(ref _refCount) == 0)
                    {
                        localCache.Dispose();
                        Interlocked.CompareExchange(ref _cache, null, localCache);
                    }
                });
            });
        }
    }
}
