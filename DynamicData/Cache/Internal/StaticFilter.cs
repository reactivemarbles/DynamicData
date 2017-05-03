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
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _filter = filter;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            if (_filter == null) return _source;

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var updater = new FilteredUpdater<TObject, TKey>(new ChangeAwareCache<TObject, TKey>(), _filter);
                return _source.Select(updater.Update)
                    .NotEmpty()
                    .SubscribeSafe(observer);
            });
        }
    }
}
