using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.List.Internal
{
    internal class FilterOnProperty<TObject, TProperty> where TObject : INotifyPropertyChanged
    {
        private readonly Func<TObject, bool> _predicate;
        private readonly TimeSpan? _throttle;
        private readonly IScheduler _scheduler;
        private readonly Expression<Func<TObject, TProperty>> _propertySelector;
        private readonly IObservable<IChangeSet<TObject>> _source;

        public FilterOnProperty(IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate,  TimeSpan? throttle = null, IScheduler scheduler = null)
        {
            _source = source;
            _propertySelector = propertySelector;
            _predicate = predicate;
            _throttle = throttle;
            _scheduler = scheduler;
        }

        public IObservable<IChangeSet<TObject>> Run()
        {
            return _source.Publish(shared =>
            {
                //do not filter on initial value otherwise every object loaded will invoke a requery
                var predicateChanged = shared.WhenPropertyChanged(_propertySelector, false)
                                        .Select(_ => _predicate)
                                        .StartWith(_predicate);

                //add a throttle if specified
                if (_throttle != null)
                    predicateChanged = predicateChanged.Throttle(_throttle.Value, _scheduler ?? Scheduler.Default);
                
                // filter all in source, based on match funcs that update on prop change
                return shared.Filter(predicateChanged);
            });
        }
    }
}
