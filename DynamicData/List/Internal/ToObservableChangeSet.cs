using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class ToObservableChangeSet<T>
    {
        private readonly IObservable<IEnumerable<T>> _source;
        private readonly Func<T, TimeSpan?> _expireAfter;
        private readonly int _limitSizeTo;
        private readonly IScheduler _scheduler;

        public ToObservableChangeSet(IObservable<T> source, 
            Func<T, TimeSpan?> expireAfter,
            int limitSizeTo,
            IScheduler scheduler = null)
             : this(source.Select(t => new[] { t }),  expireAfter, limitSizeTo, scheduler)
        {
        }

        public ToObservableChangeSet(IObservable<IEnumerable<T>> source,
            Func<T, TimeSpan?> expireAfter,
            int limitSizeTo,
            IScheduler scheduler = null)
        {
            _source = source;
            _expireAfter = expireAfter;
            _limitSizeTo = limitSizeTo;
            _scheduler = scheduler ?? Scheduler.Default;
        }


        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                long orderItemWasAdded = -1;
                var locker = new object();

                var sourceList = new ChangeAwareList<ExpirableItem<T>>();

                var sizeLimited = _source.Synchronize(locker)
                    .Scan(sourceList, (state, latest) =>
                    {
                        var items = latest.AsArray();
                        var expirable = items.Select(t => CreateExpirableItem(t, ref orderItemWasAdded));

                        if (items.Length == 1)
                        {
                            sourceList.Add(expirable);
                        }
                        else
                        {
                            sourceList.AddRange(expirable);
                        }

                        if (_limitSizeTo > 0 && state.Count > _limitSizeTo)
                        {
                            //remove oldest items [these will always be the first x in the list]
                            var toRemove = state.Count - _limitSizeTo;
                            state.RemoveRange(0, toRemove);
                        }
                        return state;
                    })
                    .Select(state => state.CaptureChanges())
                    .Publish();

                var timeLimited = (_expireAfter == null ? Observable.Never<ChangeSet<ExpirableItem<T>>>() : sizeLimited)
                    .Filter(ei => ei.ExpireAt != DateTime.MaxValue)
                    .GroupWithImmutableState(ei => ei.ExpireAt)
                    .MergeMany(grouping => 
                    {
                        var expireAt = grouping.Key.Subtract(_scheduler.Now.DateTime);
                        return Observable.Timer(expireAt, _scheduler).Select(_ => grouping);
                    })
                    .Synchronize(locker)
                    .Select(grouping =>
                    {
                        sourceList.RemoveMany(grouping.Items);
                        return sourceList.CaptureChanges();
                    });

                var publisher = sizeLimited
                    .Merge(timeLimited)
                    .Cast(ei => ei.Item)
                    .NotEmpty()
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher,  sizeLimited.Connect());
            });
        }

        private ExpirableItem<T> CreateExpirableItem(T latest, ref long orderItemWasAdded)
        {
            //check whether expiry has been set for any items
            var dateTime = _scheduler.Now.DateTime;
            var removeAt = _expireAfter?.Invoke(latest);
            var expireAt = removeAt.HasValue ? dateTime.Add(removeAt.Value) : DateTime.MaxValue;

            return new ExpirableItem<T>(latest, expireAt, Interlocked.Increment(ref orderItemWasAdded));
        }
    }
}
