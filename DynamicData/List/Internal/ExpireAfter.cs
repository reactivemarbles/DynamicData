using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class ExpireAfter<T>
    {
        private readonly ISourceList<T> _sourceList;
        private readonly Func<T, TimeSpan?> _expireAfter;
        private readonly TimeSpan? _pollingInterval;
        private readonly IScheduler _scheduler;
        private readonly object _locker;

        public   ExpireAfter([NotNull] ISourceList<T> sourceList, [NotNull] Func<T, TimeSpan?> expireAfter, TimeSpan? pollingInterval, [NotNull] IScheduler scheduler, object locker)
        {
            if (sourceList == null) throw new ArgumentNullException(nameof(sourceList));
            if (expireAfter == null) throw new ArgumentNullException(nameof(expireAfter));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            _sourceList = sourceList;
            _expireAfter = expireAfter;
            _pollingInterval = pollingInterval;
            _scheduler = scheduler;
            _locker = locker;
        }

        public IObservable<IEnumerable<T>> Run()
        {
            return Observable.Create<IEnumerable<T>>(observer =>
            {
                var dateTime = _scheduler.Now.DateTime;
                long orderItemWasAdded = -1;

                var autoRemover = _sourceList.Connect()
                                             .Synchronize(_locker)
                                             .Do(x => dateTime = _scheduler.Now.DateTime)
                                             .Cast(t =>
                                             {
                                                 var removeAt = _expireAfter(t);
                                                 var expireAt = removeAt.HasValue ? dateTime.Add(removeAt.Value) : DateTime.MaxValue;
                                                 return new ExpirableItem<T>(t, expireAt, Interlocked.Increment(ref orderItemWasAdded));
                                             })
                                             .AsObservableList();

                Action removalAction = () =>
                {
                    try
                    {
                        lock (_locker)
                        {
                            var toRemove = autoRemover.Items
                                                      .Where(ei => ei.ExpireAt <= _scheduler.Now.DateTime)
                                                      .Select(ei => ei.Item)
                                                      .ToList();

                            observer.OnNext(toRemove);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                };

                var removalSubscripion = new SingleAssignmentDisposable();
                if (_pollingInterval.HasValue)
                {
                    // use polling
                    removalSubscripion.Disposable = _scheduler.ScheduleRecurringAction(_pollingInterval.Value, removalAction);
                }
                else
                {
                    //create a timer for each distinct time
                    removalSubscripion.Disposable = autoRemover.Connect()
                                                               .DistinctValues(ei => ei.ExpireAt)
                                                               .SubscribeMany(datetime =>
                                                               {
                                                                   //  Console.WriteLine("Set expiry for {0}. Now={1}", datetime, DateTime.Now);
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
    }
}
