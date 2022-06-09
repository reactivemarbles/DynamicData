// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal class ToObservableChangeSet<T>
{
    private readonly Func<T, TimeSpan?>? _expireAfter;

    private readonly int _limitSizeTo;

    private readonly IScheduler _scheduler;

    private readonly IObservable<IEnumerable<T>> _source;

    public ToObservableChangeSet(IObservable<T> source, Func<T, TimeSpan?>? expireAfter, int limitSizeTo, IScheduler? scheduler = null)
        : this(source.Select(t => new[] { t }), expireAfter, limitSizeTo, scheduler)
    {
    }

    public ToObservableChangeSet(IObservable<IEnumerable<T>> source, Func<T, TimeSpan?>? expireAfter, int limitSizeTo, IScheduler? scheduler = null)
    {
        _source = source;
        _expireAfter = expireAfter;
        _limitSizeTo = limitSizeTo;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = new object();

                var dataSource = new SourceList<T>();

                // load local data source with current items
                var populator = _source.Synchronize(locker)
                    .Subscribe(items =>
                    {
                        dataSource.Edit(innerList =>
                        {
                            innerList.AddRange(items);

                            if (_limitSizeTo > 0 && innerList.Count > _limitSizeTo)
                            {
                                // remove oldest items [these will always be the first x in the list]
                                var toRemove = innerList.Count - _limitSizeTo;
                                innerList.RemoveRange(0, toRemove);
                            }
                        });

                    }, observer.OnError);

                // handle time expiration
                var timeExpiryDisposer = new CompositeDisposable();

                DateTime Trim(DateTime date, long ticks) => new(date.Ticks - (date.Ticks % ticks), date.Kind);

                if (_expireAfter is not null)
                {
                    var expiry = dataSource.Connect()
                        .Transform(t =>
                        {
                            var removeAt = _expireAfter?.Invoke(t);

                            if (removeAt is null)
                                return (Item: t, ExpireAt: DateTime.MaxValue);

                            // get absolute expiry, and round by milliseconds to we can attempt to batch as many items into a single group
                            var expireTime = Trim(_scheduler.Now.UtcDateTime.Add(removeAt.Value), TimeSpan.TicksPerMillisecond);

                            return (Item: t, ExpireAt: expireTime);
                        })
                        .Filter(ei => ei.ExpireAt != DateTime.MaxValue)
                        .GroupWithImmutableState(ei => ei.ExpireAt)
                        .MergeMany(grouping => Observable.Timer(grouping.Key, _scheduler).Select(_ => grouping.Items.Select(x => x.Item).ToArray()))
                        .Synchronize(locker)
                        .Subscribe(items =>
                        {
                            dataSource.RemoveMany(items);
                        });

                    timeExpiryDisposer.Add(expiry);
                }

                return new CompositeDisposable(
                    dataSource,
                    populator,
                    timeExpiryDisposer,
                    dataSource.Connect().SubscribeSafe(observer));
            });
}
