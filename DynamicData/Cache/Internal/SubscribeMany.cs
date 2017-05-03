using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class SubscribeMany<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TKey, IDisposable> _subscriptionFactory;

        public SubscribeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IDisposable> subscriptionFactory)
        {
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));

            _source = source ?? throw new ArgumentNullException(nameof(source));
            _subscriptionFactory = (t, key) => subscriptionFactory(t);
        }

        public SubscribeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        var published = _source.Publish();
                        var subscriptions = published
                            .Transform((t, k) => new SubscriptionContainer<TObject, TKey>(t, k, _subscriptionFactory))
                            .DisposeMany()
                            .Subscribe();

                        return new CompositeDisposable(
                            subscriptions, 
                            published.SubscribeSafe(observer),
                            published.Connect());
                    });
        }
    }
}