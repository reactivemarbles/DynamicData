using System;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class MergeManyItems<TObject, TKey, TDestination>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;

        public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
        }

        public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        {
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            _source = source ?? throw new ArgumentNullException(nameof(source));
            _observableSelector = (t, key) => observableSelector(t);
        }

        public IObservable<ItemWithValue<TObject, TDestination>> Run()
        {
            return Observable.Create<ItemWithValue<TObject, TDestination>>
                (
                    observer => _source.SubscribeMany((t, v) => _observableSelector(t, v)
                        .Select(z => new ItemWithValue<TObject, TDestination>(t, z))
                        .SubscribeSafe(observer))
                        .Subscribe()
                );
        }
    }
}