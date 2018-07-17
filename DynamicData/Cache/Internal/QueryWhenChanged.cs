using System;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Cache.Internal
{
    internal class QueryWhenChanged<TObject, TKey, TValue>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, IObservable<TValue>> _itemChangedTrigger;

        public QueryWhenChanged([NotNull] IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> itemChangedTrigger = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _itemChangedTrigger = itemChangedTrigger;
        }

        public IObservable<IQuery<TObject, TKey>> Run()
        {
            if (_itemChangedTrigger == null)
            {
                return _source
                    .Scan((Cache<TObject, TKey>)null, (cache, changes) =>
                    {
                        if (cache == null)
                            cache = new Cache<TObject, TKey>(changes.Count);
                        cache.Clone(changes);
                        return cache;
                    }).Select(list => new AnonymousQuery<TObject, TKey>(list));
            }

            return _source.Publish(shared =>
            {
                var locker = new object();
                var state = new Cache<TObject, TKey>();

                var inlineChange = shared
                    .MergeMany(_itemChangedTrigger)
                    .Synchronize(locker)
                    .Select(_ => new AnonymousQuery<TObject, TKey>(state));

                var sourceChanged = shared
                    .Synchronize(locker)
                    .Scan(state, (list, changes) =>
                    {
                        list.Clone(changes);
                        return list;
                    }).Select(list => new AnonymousQuery<TObject, TKey>(list));
                
                return sourceChanged.Merge(inlineChange);
            });
        }
    }
}