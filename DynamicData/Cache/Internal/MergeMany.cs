using System;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class MergeMany<TObject, TKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;

        public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            _source = source;
            _observableSelector = observableSelector;
        }

        public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            _source = source;
            _observableSelector = (t, key) => observableSelector(t);
        }

        public IObservable<TDestination> Run()
        {
            return Observable.Create<TDestination>
                (
                    observer => _source.SubscribeMany((t, key) => _observableSelector(t, key)
                        .SubscribeSafe(observer))
                        .Subscribe());
        }
    }
}