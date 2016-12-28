using System;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.List.Internal
{
    internal class MergeMany<T, TDestination>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly Func<T, IObservable<TDestination>> _observableSelector;

        public MergeMany([NotNull] IObservable<IChangeSet<T>> source,
                         [NotNull] Func<T, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            _source = source;
            _observableSelector = observableSelector;
        }

        public IObservable<TDestination> Run()
        {
            return Observable.Create<TDestination>
                (
                    observer =>
                    {
                        var locker = new object();
                        return _source
                            .SubscribeMany(t => _observableSelector(t).Synchronize(locker).Subscribe(observer.OnNext))
                            .Subscribe(t => { }, observer.OnError);
                    });
        }
    }
}
