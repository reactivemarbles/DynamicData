using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal
{
    internal class QueryWhenChanged<TObject, TKey, TValue>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, IObservable<TValue>> _itemChangedTrigger;

        public QueryWhenChanged([NotNull] IObservable<IChangeSet<TObject, TKey>> source,
                    [NotNull] Func<TObject, IObservable<TValue>> itemChangedTrigger = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
            _itemChangedTrigger = itemChangedTrigger;
        }

        public IObservable<IQuery<TObject, TKey>> Run()
        {
            return Observable.Create<IQuery<TObject, TKey>>(observer =>
            {
                var locker = new object();
                var cache = new Cache<TObject, TKey>();
                var query = new AnonymousQuery<TObject, TKey>(cache);

                if (_itemChangedTrigger != null)
                {
                    return _source.Publish(shared =>
                    {
                        var inlineChange = shared.MergeMany(_itemChangedTrigger)
                            .Synchronize(locker)
                            .Select(_ => query);

                        var sourceChanged = shared
                            .Synchronize(locker)
                            .Do(changes => cache.Clone(changes))
                            .Select(changes => query);

                        return sourceChanged.Merge(inlineChange);
                    }).SubscribeSafe(observer);
                }
                else
                {
                    return _source.Do(changes => cache.Clone(changes))
                        .Select(changes => query)
                        .SubscribeSafe(observer);
                }
            });
        }
    }

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