using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Annotations;

namespace DynamicData.Internal
{
    internal sealed class LimitSizeTo<T>
    {
        private readonly ISourceList<T> _sourceList;
        private readonly IScheduler _scheduler;
        private readonly object _locker;
        private readonly int _sizeLimit;

        public LimitSizeTo([NotNull] ISourceList<T> sourceList, int sizeLimit, [NotNull] IScheduler scheduler, object locker)
        {
            if (sourceList == null) throw new ArgumentNullException(nameof(sourceList));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
            _sourceList = sourceList;
            _sizeLimit = sizeLimit;
            _scheduler = scheduler;
            _locker = locker;
        }

        public IObservable<IEnumerable<T>> Run()
        {
            return Observable.Create<IEnumerable<T>>(observer =>
            {
                var list = new List<ExpirableItem<T>>();
                long orderItemWasAdded = -1;

                return _sourceList.Connect()
                        .ObserveOn(_scheduler)
                        .Synchronize(_locker)
                        .Convert(t => new ExpirableItem<T>(t, DateTime.Now, Interlocked.Increment(ref orderItemWasAdded)))
                        .Finally(observer.OnCompleted)
                        .Clone(list)
                        .Select(changes =>
                        {
                            var numbertoExpire = list.Count - _sizeLimit;
                            var dueForExpiry = list.OrderBy(exp => exp.ExpireAt).ThenBy(exp => exp.Index)
                                .Take(numbertoExpire)
                                .Select(item => item.Item)
                                .ToList();
                            return dueForExpiry;
                        }).Where(items => items.Count != 0)
                        .SubscribeSafe(observer);
            });

        }
}}