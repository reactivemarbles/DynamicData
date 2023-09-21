// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal class TimeExpirer<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly TimeSpan? _interval;

    private readonly IScheduler _scheduler;

    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    private readonly Func<TObject, TimeSpan?> _timeSelector;

    public TimeExpirer(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TimeSpan?> timeSelector, TimeSpan? interval, IScheduler scheduler)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _timeSelector = timeSelector ?? throw new ArgumentNullException(nameof(timeSelector));
        _interval = interval;
        _scheduler = scheduler;
    }

    public IObservable<IChangeSet<TObject, TKey>> ExpireAfter()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var cache = new IntermediateCache<TObject, TKey>(_source);

                var published = cache.Connect().Publish();
                var subscriber = published.SubscribeSafe(observer);

                var autoRemover = published.ForExpiry(_timeSelector, _interval, _scheduler).Finally(observer.OnCompleted).Subscribe(
                    keys =>
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

                return Disposable.Create(
                    () =>
                    {
                        connected.Dispose();
                        subscriber.Dispose();
                        autoRemover.Dispose();
                        cache.Dispose();
                    });
            });
    }

    public IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ForExpiry()
    {
        return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(
            observer =>
            {
                var dateTime = DateTime.Now;

                var autoRemover = _source.Do(_ => dateTime = _scheduler.Now.UtcDateTime).Transform(
                    (t, v) =>
                    {
                        var removeAt = _timeSelector(t);
                        var expireAt = removeAt.HasValue ? dateTime.Add(removeAt.Value) : DateTime.MaxValue;
                        return new ExpirableItem<TObject, TKey>(t, v, expireAt);
                    }).AsObservableCache();

                void RemovalAction()
                {
                    try
                    {
                        var toRemove = autoRemover.KeyValues.Where(kv => kv.Value.ExpireAt <= _scheduler.Now.UtcDateTime).ToList();

                        observer.OnNext(toRemove.Select(kv => new KeyValuePair<TKey, TObject>(kv.Key, kv.Value.Value)).ToList());
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                }

                var removalSubscription = new SingleAssignmentDisposable();
                if (_interval.HasValue)
                {
                    // use polling
                    removalSubscription.Disposable = _scheduler.ScheduleRecurringAction(_interval.Value, RemovalAction);
                }
                else
                {
                    // create a timer for each distinct time
                    removalSubscription.Disposable = autoRemover.Connect().DistinctValues(ei => ei.ExpireAt).SubscribeMany(
                        datetime =>
                        {
                            var expireAt = datetime.Subtract(_scheduler.Now.UtcDateTime);
                            return Observable.Timer(expireAt, _scheduler).Take(1).Subscribe(_ => RemovalAction());
                        }).Subscribe();
                }

                return Disposable.Create(
                    () =>
                    {
                        removalSubscription.Dispose();
                        autoRemover.Dispose();
                    });
            });
    }
}
