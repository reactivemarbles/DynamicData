using System;
using System.Collections.Immutable;
using System.Linq;

using BenchmarkDotNet.Attributes;

using Bogus;

namespace DynamicData.Benchmarks.List;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class ExpireAfter_List
{
    public ExpireAfter_List()
    {
        // Not exercising Refresh, since SourceList<> doesn't support it.
        _changeReasons =
        [
            ListChangeReason.Add,
            ListChangeReason.AddRange,
            ListChangeReason.Clear,
            ListChangeReason.Moved,
            ListChangeReason.Remove,
            ListChangeReason.RemoveRange,
            ListChangeReason.Replace
        ];

        // Weights are chosen to make the list size likely to grow over time,
        // exerting more pressure on the system the longer the benchmark runs,
        // while still ensuring that at least a few clears are executed.
        // Also, to prevent bogus operations (E.G. you can't remove an item from an empty list).
        _changeReasonWeightsWhenCountIs0 =
        [
            0.5f,   // Add
            0.5f,   // AddRange
            0.0f,   // Clear
            0.0f,   // Moved
            0.0f,   // Remove
            0.0f,   // RemoveRange
            0.0f    // Replace
        ];

        _changeReasonWeightsWhenCountIs1 =
        [
            0.250f, // Add
            0.250f, // AddRange
            0.001f, // Clear
            0.000f, // Moved
            0.150f, // Remove
            0.150f, // RemoveRange
            0.199f  // Replace
        ];

        _changeReasonWeightsOtherwise =
        [
            0.200f, // Add
            0.200f, // AddRange
            0.001f, // Clear
            0.149f, // Moved
            0.150f, // Remove
            0.150f, // RemoveRange
            0.150f  // Replace
        ];

        _editCount = 1_000;
        _maxChangeCount = 10;
        _maxRangeSize = 50;

        var randomizer = new Randomizer(1234567);

        var minItemLifetime = TimeSpan.FromMilliseconds(1);
        var maxItemLifetime = TimeSpan.FromMilliseconds(10);
        _items = Enumerable.Range(1, _editCount * _maxChangeCount * _maxRangeSize)
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
        using var source = new SourceList<Item>();

        using var subscription = source
            .ExpireAfter(
                timeSelector:       static item => item.Lifetime,
                pollingInterval:    null)
            .Subscribe();

        PerformRandomizedEdits(source);

        subscription.Dispose();
    }

    private void PerformRandomizedEdits(SourceList<Item> source)
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
                        1   => _changeReasonWeightsWhenCountIs1,
                        _   => _changeReasonWeightsOtherwise
                    });

                    switch (changeReason)
                    {
                        case ListChangeReason.Add:
                            updater.Add(_items[nextItemIndex++]);
                            break;

                        case ListChangeReason.AddRange:
                            updater.AddRange(Enumerable
                                .Range(0, randomizer.Int(1, _maxRangeSize))
                                .Select(_ => _items[nextItemIndex++]));
                            break;

                        case ListChangeReason.Replace:
                            updater.Replace(
                                original: randomizer.ListItem(updater),
                                replaceWith: _items[nextItemIndex++]);
                            break;

                        case ListChangeReason.Remove:
                            updater.RemoveAt(randomizer.Int(0, updater.Count - 1));
                            break;

                        case ListChangeReason.RemoveRange:
                            var removeCount = randomizer.Int(1, Math.Min(_maxRangeSize, updater.Count));
                            updater.RemoveRange(
                                index: randomizer.Int(0, updater.Count - removeCount),
                                count: removeCount);
                            break;

                        case ListChangeReason.Moved:
                            int originalIndex;
                            int destinationIndex;

                            do
                            {
                                originalIndex = randomizer.Int(0, updater.Count - 1);
                                destinationIndex = randomizer.Int(0, updater.Count - 1);
                            } while (originalIndex == destinationIndex);

                            updater.Move(originalIndex, destinationIndex);
                            break;

                        case ListChangeReason.Clear:
                            updater.Clear();
                            break;
                    }
                }
            });
        }
    }

    private readonly ListChangeReason[]     _changeReasons;
    private readonly float[]                _changeReasonWeightsOtherwise;
    private readonly float[]                _changeReasonWeightsWhenCountIs0;
    private readonly float[]                _changeReasonWeightsWhenCountIs1;
    private readonly int                    _editCount;
    private readonly ImmutableArray<Item>   _items;
    private readonly int                    _maxChangeCount;
    private readonly int                    _maxRangeSize;

    private sealed record Item
    {
        public required int Id { get; init; }

        public required TimeSpan? Lifetime { get; init; }
    }
}
