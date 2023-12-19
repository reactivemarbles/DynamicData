using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using Bogus;

namespace DynamicData.Tests.Utilities;

internal static class FakerExtensions
{
    public static IObservable<T> IntervalGenerate<T>(this Faker<T> faker, Randomizer randomizer, TimeSpan maxTime, IScheduler? scheduler = null)
         where T : class =>
            randomizer.Interval(maxTime, scheduler).Select(_ => faker.Generate());

    public static IObservable<T> IntervalGenerate<T>(this Faker<T> faker, TimeSpan period, IScheduler? scheduler = null)
         where T : class =>
            Observable.Interval(period, scheduler ?? DefaultScheduler.Instance).Select(_ => faker.Generate());
}
