using System;
using System.Collections.Immutable;
using System.Linq;

using BenchmarkDotNet.Attributes;

using Bogus;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ExpireAfter_Cache_ForSource
{
    public ExpireAfter_Cache_ForSource()
    {
        // Not exercising Moved, since SourceCache<> doesn't support it.
        _changeReasons =
        [
            ChangeReason.Add,
            ChangeReason.Refresh,
            ChangeReason.Remove,
            ChangeReason.Update
        ];

        // Weights are chosen to make the cache size likely to grow over time,
        // exerting more pressure on the system the longer the benchmark runs.
        // Also, to prevent bogus operations (E.G. you can't remove an item from an empty cache).
        _changeReasonWeightsWhenCountIs0 =
        [
            1f, // Add
            0f, // Refresh
            0f, // Remove
            0f  // Update
        ];

        _changeReasonWeightsOtherwise =
        [
            0.30f, // Add
            0.25f, // Refresh
            0.20f, // Remove
            0.25f  // Update
        ];

        _editCount = 5_000;
        _maxChangeCount = 20;

        var randomizer = new Randomizer(1234567);

        var minItemLifetime = TimeSpan.FromMilliseconds(1);
        var maxItemLifetime = TimeSpan.FromMilliseconds(10);
        _items = Enumerable.Range(1, _editCount * _maxChangeCount)
            .Select(id => new Item()
            {
                Id          = id,
                Lifetime    = randomizer.Bool()
                    ? TimeSpan.FromTicks(randomizer.Long(minItemLifetime.Ticks, maxItemLifetime.Ticks))
                    : null
            })
            .ToImmutableArray();
    }

    [Benchmark]
    public void RandomizedEditsAndExpirations()
    {
        using var source = new SourceCache<Item, int>(static item => item.Id);

        using var subscription = source
            .ExpireAfter(
                timeSelector:       static item => item.Lifetime,
                pollingInterval:    null)
            .Subscribe();

        PerformRandomizedEdits(source);

        subscription.Dispose();
    }

    private void PerformRandomizedEdits(SourceCache<Item, int> source)
    {
        var randomizer = new Randomizer(1234567);
        
        var nextItemIndex = 0;

        for (var i = 0; i < _editCount; ++i)
        {
            source.Edit(updater =>
            {
                var changeCount = randomizer.Int(1, _maxChangeCount);
                for (var i = 0; i < changeCount; ++i)
                {
                    var changeReason = randomizer.WeightedRandom(_changeReasons, updater.Count switch
                    {
                        0   => _changeReasonWeightsWhenCountIs0,
                        _   => _changeReasonWeightsOtherwise
                    });

                    switch (changeReason)
                    {
                        case ChangeReason.Add:
                            updater.AddOrUpdate(_items[nextItemIndex++]);
                            break;

                        case ChangeReason.Refresh:
                            updater.Refresh(updater.Keys.ElementAt(randomizer.Int(0, updater.Count - 1)));
                            break;

                        case ChangeReason.Remove:
                            updater.RemoveKey(updater.Keys.ElementAt(randomizer.Int(0, updater.Count - 1)));
                            break;

                        case ChangeReason.Update:
                            updater.AddOrUpdate(updater.Items.ElementAt(randomizer.Int(0, updater.Count - 1)));
                            break;
                    }
                }
            });
        }
    }

    private readonly ChangeReason[]         _changeReasons;
    private readonly float[]                _changeReasonWeightsOtherwise;
    private readonly float[]                _changeReasonWeightsWhenCountIs0;
    private readonly int                    _editCount;
    private readonly ImmutableArray<Item>   _items;
    private readonly int                    _maxChangeCount;

    private sealed record Item
    {
        public required int Id { get; init; }

        public required TimeSpan? Lifetime { get; init; }
    }
}
