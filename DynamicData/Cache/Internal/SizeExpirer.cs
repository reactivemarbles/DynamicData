using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class SizeExpirer<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly int _size;

        public SizeExpirer(IObservable<IChangeSet<TObject, TKey>> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentException("Size limit must be greater than zero");

            _source = source;
            _size = size;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var sizeLimiter = new SizeLimiter<TObject, TKey>(_size);
                var root = new IntermediateCache<TObject, TKey>(_source);

                var subscriber = root.Connect()
                    .Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now))
                    .Select(changes =>
                    {
                        var result = sizeLimiter.Change(changes);

                        var removes = result.Where(c => c.Reason == ChangeReason.Remove);
                        root.Edit(updater => removes.ForEach(c => updater.Remove((TKey)c.Key)));
                        return result;
                    })
                    .FinallySafe(observer.OnCompleted)
                    .SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();
                    root.Dispose();
                });
            });
        }
    }
}