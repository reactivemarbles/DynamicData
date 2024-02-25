using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

using Bogus;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ExpireAfter_Cache_ForStream
{
    public ExpireAfter_Cache_ForStream()
    {
        // Not exercising Moved, since ChangeAwareCache<> doesn't support it, and I'm too lazy to implement it by hand.
        var changeReasons = new[]
        {
            ChangeReason.Add,
            ChangeReason.Refresh,
            ChangeReason.Remove,
            ChangeReason.Update
        };

        // Weights are chosen to make the cache size likely to grow over time,
        // exerting more pressure on the system the longer the benchmark runs.
        // Also, to prevent bogus operations (E.G. you can't remove an item from an empty cache).
        var changeReasonWeightsWhenCountIs0 = new[]
        {
            1f, // Add
            0f, // Refresh
            0f, // Remove
            0f  // Update
        };

        var changeReasonWeightsOtherwise = new[]
        {
            0.30f, // Add
            0.25f, // Refresh
            0.20f, // Remove
            0.25f  // Update
        };

        var maxChangeCount = 20;
        var minItemLifetime = TimeSpan.FromMilliseconds(1);
        var maxItemLifetime = TimeSpan.FromMilliseconds(10);

        var randomizer = new Randomizer(1234567);

        var nextItemId = 1;

        var changeSets = ImmutableArray.CreateBuilder<IChangeSet<Item, int>>(initialCapacity: 5_000);

        var cache = new ChangeAwareCache<Item, int>();

        while (changeSets.Count < changeSets.Capacity)
        {
            var changeCount = randomizer.Int(1, maxChangeCount);
            for (var i = 0; i < changeCount; ++i)
            {
                var changeReason = randomizer.WeightedRandom(changeReasons, cache.Count switch
                {
                    0   => changeReasonWeightsWhenCountIs0,
                    _   => changeReasonWeightsOtherwise
                });

                switch (changeReason)
                {
                    case ChangeReason.Add:
                        cache.AddOrUpdate(
                            item:   new Item()
                            {
                                Id          = nextItemId,
                                Lifetime    = randomizer.Bool()
                                    ? TimeSpan.FromTicks(randomizer.Long(minItemLifetime.Ticks, maxItemLifetime.Ticks))
                                    : null
                            },
                            key:    nextItemId);
                        ++nextItemId;
                        break;

                    case ChangeReason.Refresh:
                        cache.Refresh(cache.Keys.ElementAt(randomizer.Int(0, cache.Count - 1)));
                        break;

                    case ChangeReason.Remove:
                        cache.Remove(cache.Keys.ElementAt(randomizer.Int(0, cache.Count - 1)));
                        break;

                    case ChangeReason.Update:
                        var id = cache.Keys.ElementAt(randomizer.Int(0, cache.Count - 1));
                        cache.AddOrUpdate(
                            item:   new Item()
                            {
                                Id          = id,
                                Lifetime    = randomizer.Bool()
                                    ? TimeSpan.FromTicks(randomizer.Long(minItemLifetime.Ticks, maxItemLifetime.Ticks))
                                    : null
                            },
                            key:    id);
                        break;
                }
            }

            changeSets.Add(cache.CaptureChanges());
        }

        _changeSets = changeSets.MoveToImmutable();
    }

    [Benchmark]
    public void RandomizedEditsAndExpirations()
    {
        using var source = new Subject<IChangeSet<Item, int>>();

        using var subscription = source
            .ExpireAfter(
                timeSelector:       static item => item.Lifetime,
                pollingInterval:    null)
            .Subscribe();

        foreach (var changeSet in _changeSets)
            source.OnNext(changeSet);

        subscription.Dispose();
    }

    private readonly ImmutableArray<IChangeSet<Item, int>> _changeSets;

    private sealed record Item
    {
        public required int Id { get; init; }

        public required TimeSpan? Lifetime { get; init; }
    }
}
