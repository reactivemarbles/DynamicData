using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.Tests.Utilities;

/// <summary>
/// Extra Observable Tools for Testing
/// </summary>
internal static class ObservableEx
{
    /// <summary>
    /// Like <see cref="Observable.Interval(TimeSpan, IScheduler)"/> except the interval time is recomputed each time.
    /// </summary>
    /// <param name="nextInterval">Function to get the next interval.</param>
    /// <param name="scheduler">Scheduler to use for firing events</param>
    /// <returns>IObservable{long} instance.</returns>
    public static IObservable<long> Interval(Func<TimeSpan> nextInterval, IScheduler? scheduler = null) =>
        Observable.Create<long>(observer =>
        {
            _ = nextInterval ?? throw new ArgumentNullException(nameof(nextInterval));

            IDisposable ScheduleFirst(IScheduler sch)
            {
                IDisposable HandleNext(IScheduler _, long counter)
                {
                    observer.OnNext(counter);
                    return ScheduleNext(sch, counter + 1);
                }

                IDisposable ScheduleNext(IScheduler _, long counter) => sch.Schedule(counter, nextInterval(), HandleNext);

                return sch.Schedule<long>(0, nextInterval(), HandleNext);
            }

            return ScheduleFirst(scheduler ?? DefaultScheduler.Instance);
        });
}
