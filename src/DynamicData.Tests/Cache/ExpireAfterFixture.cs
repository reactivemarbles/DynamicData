using System;
using System.Reactive.Concurrency;

using Microsoft.Reactive.Testing;

namespace DynamicData.Tests.Cache;

public static partial class ExpireAfterFixture
{
    private static TestScheduler CreateTestScheduler()
    {
        var scheduler = new TestScheduler();
        scheduler.AdvanceTo(DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks);

        return scheduler;
    }

    private static Func<Item, TimeSpan?> CreateTimeSelector(IScheduler scheduler)
        => item => item.Expiration - scheduler.Now;

    private class Item
    {
        public required int Id { get; init; }

        public DateTimeOffset? Expiration { get; set; }
    }
}
