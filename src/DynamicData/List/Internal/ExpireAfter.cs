// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class ExpireAfter<T>(ISourceList<T> sourceList, Func<T, TimeSpan?> expireAfter, TimeSpan? pollingInterval, IScheduler scheduler, object locker)
    where T : notnull
{
    private readonly Func<T, TimeSpan?> _expireAfter = expireAfter ?? throw new ArgumentNullException(nameof(expireAfter));
    private readonly IScheduler _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

    private readonly ISourceList<T> _sourceList = sourceList ?? throw new ArgumentNullException(nameof(sourceList));

    public IObservable<IEnumerable<T>> Run() => Observable.Create<IEnumerable<T>>(
            observer =>
            {
                var dateTime = _scheduler.Now.UtcDateTime;
                long orderItemWasAdded = -1;

                var autoRemover = _sourceList.Connect().Synchronize(locker).Do(_ => dateTime = _scheduler.Now.UtcDateTime).Cast(
                    t =>
                    {
                        var removeAt = _expireAfter(t);
                        var expireAt = removeAt.HasValue ? dateTime.Add(removeAt.Value) : DateTime.MaxValue;
                        return new ExpirableItem<T>(t, expireAt, Interlocked.Increment(ref orderItemWasAdded));
                    }).AsObservableList();

                void RemovalAction()
                {
                    try
                    {
                        lock (locker)
                        {
                            var toRemove = autoRemover.Items.Where(ei => ei.ExpireAt <= _scheduler.Now.DateTime).Select(ei => ei.Item).ToList();

                            observer.OnNext(toRemove);
                        }
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                }

                var removalSubscription = new SingleAssignmentDisposable();
                if (pollingInterval.HasValue)
                {
                    // use polling
                    // ReSharper disable once InconsistentlySynchronizedField
                    removalSubscription.Disposable = _scheduler.ScheduleRecurringAction(pollingInterval.Value, RemovalAction);
                }
                else
                {
                    // create a timer for each distinct time
                    removalSubscription.Disposable = autoRemover.Connect().DistinctValues(ei => ei.ExpireAt).SubscribeMany(
                        datetime =>
                        {
                            // ReSharper disable once InconsistentlySynchronizedField
                            var expireAt = datetime.Subtract(_scheduler.Now.UtcDateTime);

                            // ReSharper disable once InconsistentlySynchronizedField
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
