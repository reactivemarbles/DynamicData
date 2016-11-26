using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class TimeExpirer<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TimeSpan?> _timeSelector;
        private readonly TimeSpan? _interval;
        private readonly IScheduler _scheduler;

        public TimeExpirer(IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TimeSpan?> timeSelector,
            TimeSpan? interval,
            IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));

            _source = source;
            _timeSelector = timeSelector;
            _interval = interval;
            _scheduler = scheduler ?? Scheduler.Default;
        }

        public IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ForExpiry()
        {
            return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(observer =>
            {
                var dateTime = DateTime.Now;

                var autoRemover = _source
                    .Do(x => dateTime = _scheduler.Now.DateTime)
                    .Transform((t, v) =>
                    {
                        var removeAt = _timeSelector(t);
                        var expireAt = removeAt.HasValue ? dateTime.Add(removeAt.Value) : DateTime.MaxValue;
                        return new ExpirableItem<TObject, TKey>(t, v, expireAt);
                    })
                    .AsObservableCache();

                Action removalAction = () =>
                {
                    try
                    {
                        var toRemove = autoRemover.KeyValues
                            .Where(kv => kv.Value.ExpireAt <= _scheduler.Now.DateTime)
                            .ToList();

                        observer.OnNext(toRemove.Select(kv => new KeyValuePair<TKey, TObject>(kv.Key, kv.Value.Value)).ToList());
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                };

                var removalSubscripion = new SingleAssignmentDisposable();
                if (_interval.HasValue)
                {
                    // use polling
                    removalSubscripion.Disposable = _scheduler.ScheduleRecurringAction(_interval.Value, removalAction);
                }
                else
                {
                    //create a timer for each distinct time
                    removalSubscripion.Disposable = autoRemover.Connect()
                        .DistinctValues(ei => ei.ExpireAt)
                        .SubscribeMany(datetime =>
                        {
                            var expireAt = datetime.Subtract(_scheduler.Now.DateTime);
                            return Observable.Timer(expireAt, _scheduler)
                                .Take(1)
                                .Subscribe(_ => removalAction());
                        })
                        .Subscribe();
                }
                return Disposable.Create(() =>
                {
                    removalSubscripion.Dispose();
                    autoRemover.Dispose();
                });
            });
        }

        public IObservable<IChangeSet<TObject, TKey>> ExpireAfter()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var cache = new IntermediateCache<TObject, TKey>(_source);

                var published = cache.Connect().Publish();
                var subscriber = published.SubscribeSafe(observer);

                var autoRemover = published.ForExpiry(_timeSelector, _interval, _scheduler)
                    .FinallySafe(observer.OnCompleted)
                    .Subscribe(keys =>
                    {
                        try
                        {
                            cache.Edit(updater => updater.Remove(keys.Select(kv => kv.Key)));
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    });

                var connected = published.Connect();

                return Disposable.Create(() =>
                {
                    connected.Dispose();
                    subscriber.Dispose();
                    autoRemover.Dispose();
                    cache.Dispose();
                });
            });
        }
    }
}