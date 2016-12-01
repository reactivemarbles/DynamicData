using System;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Cache.Internal
{
    internal class QueryWhenChanged<TObject, TKey, TValue>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, IObservable<TValue>> _itemChangedTrigger;

        public QueryWhenChanged([NotNull] IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] Func<TObject, IObservable<TValue>> itemChangedTrigger = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
            _itemChangedTrigger = itemChangedTrigger;
        }

        public IObservable<IQuery<TObject, TKey>> Run()
        {
            return Observable.Create<IQuery<TObject, TKey>>(observer =>
            {
                var locker = new object();
                var cache = new Cache<TObject, TKey>();
                var query = new AnonymousQuery<TObject, TKey>(cache);

                if (_itemChangedTrigger != null)
                {
                    return _source.Publish(shared =>
                    {
                        var inlineChange = shared.MergeMany(_itemChangedTrigger)
                            .Synchronize(locker)
                            .Select(_ => query);

                        var sourceChanged = shared
                            .Synchronize(locker)
                            .Do(changes => cache.Clone(changes))
                            .Select(changes => query);

                        return sourceChanged.Merge(inlineChange);
                    }).SubscribeSafe(observer);
                }
                else
                {
                    return _source.Do(changes => cache.Clone(changes))
                        .Select(changes => query)
                        .SubscribeSafe(observer);
                }
            });
        }
    }
}