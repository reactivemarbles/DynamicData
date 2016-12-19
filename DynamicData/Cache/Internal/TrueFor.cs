using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class TrueFor<TObject, TKey, TValue>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, IObservable<TValue>> _observableSelector;
        private readonly Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> _collectionMatcher;

        public TrueFor(IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, IObservable<TValue>> observableSelector,
            Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));
            if (collectionMatcher == null) throw new ArgumentNullException(nameof(collectionMatcher));
            _source = source;
            _observableSelector = observableSelector;
            _collectionMatcher = collectionMatcher;
        }

        public IObservable<bool> Run()
        {
            return Observable.Create<bool>(observer =>
            {
                var transformed = _source.Transform(t => new ObservableWithValue<TObject, TValue>(t, _observableSelector(t))).Publish();
                var inlineChanges = transformed.MergeMany(t => t.Observable);
                var queried = transformed.QueryWhenChanged(q => q.Items);

                //nb: we do not care about the inline change because we are only monitoring it to cause a re-evalutaion of all items
                var publisher = queried.CombineLatest(inlineChanges, (items, inline) => _collectionMatcher(items))
                    .DistinctUntilChanged()
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher, transformed.Connect());
            });
        }
    }
}