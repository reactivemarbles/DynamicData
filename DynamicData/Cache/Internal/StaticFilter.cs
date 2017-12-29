using System;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class StaticFilter<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, bool> _filter;

        public StaticFilter(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        {
            _source = source;
            _filter = filter;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return _source.Scan(new ChangeAwareCache<TObject, TKey>(), (cache, changes) =>
                {
                    cache.FilterChanges(changes, _filter);
                    return cache;
                })
                .Select(cache => cache.CaptureChanges())
                .NotEmpty();
        }
    }
}
