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
    [Fact]
    public void LetsSeeWhetherWeCanRandomlyHitADifferentRaceCondition()
    {
        using var leftSource = new SourceCache<Thing, long>(thing => thing.Id);
        using var rightSource = new SourceCache<Thing, long>(thing => thing.Id);

        var resultStream = ObservableCacheEx.InnerJoin(
            left: leftSource.Connect(),
            right: rightSource.Connect(),
            rightKeySelector: rightThing => rightThing.Id,
            (keys, leftThing, rightThing) => new Thing()
            {
                Id = keys.leftKey,
                Name = $"{leftThing.Name} x {rightThing.Name}"
            });

        using var leftThingGenerator = BeginGeneratingThings(leftSource, "Left");
        using var rightThingGenerator = BeginGeneratingThings(rightSource, "Left");

        for (var i = 0; i < 100; ++i)
        {
            using var subscription = resultStream.Subscribe();
        }

        IDisposable BeginGeneratingThings(SourceCache<Thing, long> source, string namePrefix)
            // Generate items infinitely. The runtime of the test is limited by the .Subscribe() loop.
            => Observable.Range(1, int.MaxValue, ThreadPoolScheduler.Instance)
                .Subscribe(id =>
                {
                    source.AddOrUpdate(new Thing()
                    {
                        Id = id,
                        Name = $"{namePrefix}Thing #{id}"
                    });
                    // Start removing items after the first 100, to keep the overhead of calling .Subscribe() down.
                    source.RemoveKey(id - 100);
                });
    }

    public class Thing
    {
        public long Id { get; set; }

        public string? Name { get; set; }
    }
}
