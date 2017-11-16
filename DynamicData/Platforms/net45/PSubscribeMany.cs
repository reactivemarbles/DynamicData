#if P_LINQ

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData.PLinq
{
    internal class PSubscribeMany<TObject, TKey>
    {
        private readonly ParallelisationOptions _parallelisationOptions;
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TKey, IDisposable> _subscriptionFactory;
        
        public PSubscribeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IDisposable> subscriptionFactory, ParallelisationOptions parallelisationOptions)
        {

            _source = source ?? throw new ArgumentNullException(nameof(source));
            _subscriptionFactory = subscriptionFactory ?? throw new ArgumentNullException(nameof(subscriptionFactory));
            _parallelisationOptions = parallelisationOptions;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {

            return Observable.Create<IChangeSet<TObject, TKey>>
            (
                observer =>
                {
                    var published = _source.Publish();
                    var subscriptions = published
                        .Transform((t, k) => _subscriptionFactory(t, k), _parallelisationOptions)
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
#endif