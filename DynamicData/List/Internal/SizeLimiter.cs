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
        private readonly int _sizeLimit;
        private readonly IScheduler _scheduler;

        public LimitSizeTo([NotNull] ISourceList<T> sourceList, int sizeLimit, [NotNull] IScheduler scheduler)
        {
            if (sourceList == null) throw new ArgumentNullException(nameof(sourceList));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
            _sourceList = sourceList;
            _sizeLimit = sizeLimit;
            _scheduler = scheduler;
        }

        public IObservable<IEnumerable<T>> Run()
        {
            return Observable.Create<IEnumerable<T>>(observer =>
            {
                var list = new List<ExpirableItem<T>> ();
                long orderItemWasAdded = -1;
                var locker = new object();

                return _sourceList.Connect()
                        .ObserveOn(_scheduler)
                        .Synchronize(locker)
                        .Select(changes => changes.Transform(t => new ExpirableItem<T>(t, DateTime.Now, Interlocked.Increment(ref orderItemWasAdded))))
                        .FinallySafe(observer.OnCompleted)
                        .Subscribe(changes =>
                        {
                            list.Clone(changes);
                            var numbertoExpire = list.Count - _sizeLimit;
                            var dueForExpiry = list.OrderBy(exp => exp.ExpireAt).ThenBy(exp => exp.Index)
                                .Take(numbertoExpire)
                                .Select(item => item.Item)
                                .ToList();

                            if (dueForExpiry.Count>0)
                                observer.OnNext(dueForExpiry);
                        });
            });

        }
}}