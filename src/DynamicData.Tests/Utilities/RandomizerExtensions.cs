using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Bogus;

namespace DynamicData.Tests.Utilities;

internal static class RandomizerExtensions
{
    public static TimeSpan TimeSpan(this Randomizer randomizer, TimeSpan minTime, TimeSpan maxTime) => System.TimeSpan.FromTicks(randomizer.Long(minTime.Ticks, maxTime.Ticks));

    public static TimeSpan TimeSpan(this Randomizer randomizer, TimeSpan maxTime) => TimeSpan(randomizer, System.TimeSpan.Zero, maxTime);

    public static bool CoinFlip(this Randomizer randomizer, Action action)
    {
        if (randomizer.Bool())
        {
            action();
            return true;
        }

        return false;
    }

    public static bool Chance(this Randomizer randomizer, double chancePercent, Action action)
    {
        Debug.Assert(chancePercent >= 0.0 && chancePercent <= 1.0);
        if (randomizer.Double() <= chancePercent)
        {
            action();
            return true;
        }

        return false;
    }

    public static IObservable<long> Interval(this Randomizer randomizer, TimeSpan minTime, TimeSpan maxTime, IScheduler? scheduler = null) =>
        Observable.Create<long>(observer =>
        {
            IDisposable HandleNext(IScheduler sch, long counter)
            {
                observer.OnNext(counter);
                return ScheduleNext(sch, counter + 1);
            }

            IDisposable ScheduleNext(IScheduler sch, long counter) => sch.Schedule<long>(counter, randomizer.TimeSpan(minTime, maxTime), HandleNext);

            return ScheduleNext(scheduler ?? DefaultScheduler.Instance, 0);
        });

    public static IObservable<long> Interval(this Randomizer randomizer, TimeSpan maxTime, IScheduler? scheduler = null) =>
        Interval(randomizer, System.TimeSpan.Zero, maxTime, scheduler);
}
