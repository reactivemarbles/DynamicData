// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal class ToObservableChangeSet<TObject, TKey>
    where TKey : notnull
{
    private readonly IObservable<IEnumerable<TObject>> _source;
    private readonly Func<TObject, TKey> _keySelector;
    private readonly Func<TObject, TimeSpan?>? _expireAfter;
    private readonly int _limitSizeTo;
    private readonly IScheduler _scheduler;

    public ToObservableChangeSet(IObservable<TObject> source,
        Func<TObject, TKey> keySelector,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler = null)
        : this(source.Select(t => new[] { t }), keySelector, expireAfter, limitSizeTo, scheduler)
    {
    }

    public ToObservableChangeSet(IObservable<IEnumerable<TObject>> source,
        Func<TObject, TKey> keySelector,
        Func<TObject, TimeSpan?>? expireAfter,
        int limitSizeTo,
        IScheduler? scheduler = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _expireAfter = expireAfter;
        _limitSizeTo = limitSizeTo;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var locker = new object();

            var dataSource = new SourceCache<TObject, TKey>(_keySelector);

            // load local data source with current items
            var populator = _source.Synchronize(locker)
                .Subscribe(items => dataSource.AddOrUpdate(items), observer.OnError);

            // handle size expiration
            var sizeExpiryDisposer = new CompositeDisposable();

            if (_limitSizeTo > 0)
            {
                long orderItemWasAdded = -1;

                var transformed = dataSource.Connect()
                    .Transform(t => (Item: t, Order: Interlocked.Increment(ref orderItemWasAdded)))
                    .AsObservableCache();

                var transformedRemoved = transformed.Connect()
                    .Subscribe(_ =>
                    {
                        if (transformed.Count <= _limitSizeTo) return;

                        // remove oldest items
                        var itemsToRemove = transformed.KeyValues
                            .OrderBy(exp => exp.Value.Order)
                            .Take(transformed.Count - _limitSizeTo)
                            .Select(x => x.Key)
                            .ToArray();

                        // schedule, otherwise we can get a deadlock when removing due to re-entrancey
                        _scheduler.Schedule(() => dataSource.Remove(itemsToRemove));
                    });
                sizeExpiryDisposer.Add(transformed);
                sizeExpiryDisposer.Add(transformedRemoved);
            }

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
                    .MergeMany(grouping => Observable.Timer(grouping.Key, _scheduler).Select(_ => grouping))
                    .Synchronize(locker)
                    .Subscribe(grouping => dataSource.Remove(grouping.Keys));

                timeExpiryDisposer.Add(expiry);
            }

            return new CompositeDisposable(
                dataSource,
                populator,
                sizeExpiryDisposer,
                timeExpiryDisposer,
                dataSource.Connect().SubscribeSafe(observer));
        });
    }
}
