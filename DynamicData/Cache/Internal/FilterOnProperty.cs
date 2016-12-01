using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal
{
    internal class FilterOnProperty<TObject, TKey, TProperty>
        where TObject : INotifyPropertyChanged
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Expression<Func<TObject, TProperty>> _propertySelector;
        private readonly Func<TObject, bool> _predicate;

        public FilterOnProperty(IObservable<IChangeSet<TObject, TKey>> source,
            Expression<Func<TObject, TProperty>> propertySelector,
            Func<TObject, bool> predicate)
        {
            _source = source;
            _propertySelector = propertySelector;
            _predicate = predicate;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                //share the connection, otherwise the entire observable chain is duplicated 
                var shared = _source.Publish();

                //watch each property and build a new predicate when a property changed
                //do not filter on initial value otherwise every object loaded will invoke a requery
                var predicateStream = shared.WhenPropertyChanged(_propertySelector, false)
                    .Select(_ => _predicate)
                    .StartWith(_predicate);

                //requery when the above filter changes
                var changedAndMatching = shared.Filter(predicateStream);

                var publisher = changedAndMatching.SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }
    }
}