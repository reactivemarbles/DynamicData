using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;


namespace DynamicData.Tests.Utilities
{
    public static class DynamicDataExtensions
    {

        public static IObservable<IChangeSet<TObject, TKey>> FilterOnProperty<TObject, TKey, TProperty>(this IObservable<IChangeSet<TObject, TKey>> source,
            Expression<Func<TObject, TProperty>> propertySelector,
            Func<TObject, bool> predicate) where TObject : INotifyPropertyChanged
        {

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                //share the connection, otherwise the entire observable chain is duplicated 
                var shared = source.Publish();

                //watch each property and build a new predicate when a property changed
                //do not filter on initial value otherwise every object loaded will invoke a requery
                var predicateStream = shared.WhenPropertyChanged(propertySelector, false)
                                        .Select(_ => predicate)
                                        .StartWith(predicate);

                //requery when the above filter changes
                var changedAndMatching = shared.Filter(predicateStream);

                var publisher = changedAndMatching.SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }

        public static IObservable<IChangeSet<TObject>> FilterOnProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source,
            Expression<Func<TObject, TProperty>> propertySelector,
            Func<TObject, bool> predicate) where TObject : INotifyPropertyChanged
        {

            return Observable.Create<IChangeSet<TObject>>(observer =>
            {
                //share the connection, otherwise the entire observable chain is duplicated 
                var shared = source.Publish();

                //do not filter on initial value otherwise every object loaded will invoke a requery
                var predicateStream = shared.WhenPropertyChanged(propertySelector, false)
                                        .Select(_ => predicate)
                                        .StartWith(predicate);

                // filter all in source, based on match funcs that update on prop change
                var changedAndMatching = shared.Filter(predicateStream, FilterPolicy.CalculateDiffSet);

                var publisher = changedAndMatching.SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }


    }
}