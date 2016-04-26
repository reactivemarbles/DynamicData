using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal
{
    class FilterOnProperty<TObject, TProperty> where TObject: INotifyPropertyChanged
    {
        private readonly Func<TObject, bool> _predicate;
        private readonly Expression<Func<TObject, TProperty>> _propertySelector;
        private readonly IObservable<IChangeSet<TObject>> _source;

        public FilterOnProperty(IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate)
        {
            _source = source;
            _propertySelector = propertySelector;
            _predicate = predicate;
        }

        public IObservable<IChangeSet<TObject>> Run()
        {
            return Observable.Create<IChangeSet<TObject>>(observer =>
            {
                //share the connection, otherwise the entire observable chain is duplicated 
                var shared = _source.Publish();

                //do not filter on initial value otherwise every object loaded will invoke a requery
                var predicateStream = shared.WhenPropertyChanged(_propertySelector, false)
                                        .Select(_ => _predicate)
                                        .StartWith(_predicate);

                // filter all in source, based on match funcs that update on prop change
                var changedAndMatching = shared.Filter(predicateStream, FilterPolicy.CalculateDiffSet);

                var publisher = changedAndMatching.SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }
    }
}
