using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.List.Internal
{
    internal sealed class SubscribeMany<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly Func<T, IDisposable> _subscriptionFactory;

        public SubscribeMany([NotNull] IObservable<IChangeSet<T>> source, [NotNull] Func<T, IDisposable> subscriptionFactory)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>
                (
                    observer =>
                    {
                        var shared = _source.Publish();
                        var subscriptions = shared
                            .Transform(t => _subscriptionFactory(t))
                            .DisposeMany()
                            .Subscribe();

                        return new CompositeDisposable(subscriptions, shared.SubscribeSafe(observer), shared.Connect());
                    });
        }
    }
}
