using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

using Xunit;

namespace DynamicData.Tests.Cache;

public class InnerJoinFixtureRaceCondition
{
    /// <summary>
    /// Tests to see whether we have fixed a race condition. See https://github.com/reactiveui/DynamicData/issues/364
    ///
    /// RP: 04-June-2020 Before the fix, this code occasionally caused a threading issue and the fix seems to have worked.
    /// I am leaving it here for a short period of time to see whether it produces traffic light test results.
    /// </summary>
    [Fact]
    public void LetsSeeWhetherWeCanRandomlyHitARaceCondition()
    {
        var ids = ObservableChangeSet.Create<long, long>(sourceCache => { return Observable.Range(1, 1000000, Scheduler.Default).Subscribe(x => sourceCache.AddOrUpdate(x)); }, x => x);

        var itemsCache = new SourceCache<Thing, long>(x => x.Id);
        itemsCache.AddOrUpdate(
            new[]
            {
                new Thing { Id = 300, Name = "Quick" },
                new Thing { Id = 600, Name = "Brown" },
                new Thing { Id = 900, Name = "Fox" },
                new Thing { Id = 1200, Name = "Hello" },
            });

        ids.InnerJoin(itemsCache.Connect(), x => x.Id, (_, thing) => thing).Subscribe((z) => { }, ex => { }, () => { });
    }

    // See https://github.com/reactivemarbles/DynamicData/issues/787 
    [Fact(Skip = "Heavyweight test so probably best run manually if this issue comes up again")]
    public void LetsSeeWhetherWeCanRandomlyHitADifferentRaceCondition()
    {
        var leftCache = new SourceCache<Thing, long>(x => x.Id);
        var rightCache = new SourceCache<Thing, long>(x => x.Id);
        var joined = leftCache.Connect().InnerJoin(rightCache.Connect(), x => x.Id,
            (keys, leftThing, rightThing) =>
                new Thing { Id = keys.leftKey, Name = $"{leftThing.Name} x {rightThing.Name}" });

        IDisposable StartUpdating(ISourceCache<Thing, long> sourceCache) =>
            Observable.Range(1, 1_000_000, Scheduler.Default)
                .Subscribe(x => sourceCache.AddOrUpdate(new Thing { Id = x, Name = $"{x}" }));

        using var leftUpdater = StartUpdating(leftCache);
        using var rightUpdater = StartUpdating(rightCache);
        using var subscription = joined.Subscribe();
    }

    public class Thing
    {
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
