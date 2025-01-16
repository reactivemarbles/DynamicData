using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

using Bogus;

namespace DynamicData.Benchmarks.List;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class Filter_List_WithPredicateState
{
    public Filter_List_WithPredicateState()
    {
        var randomizer = new Randomizer(0x1234567);

        _changeSets = GenerateStressItemsAndChangeSets(
            editCount:      5_000,
            maxChangeCount: 20,
            maxRangeSize:   10,
            randomizer:     randomizer);
            
        _predicateStates = GenerateRandomPredicateStates(
            valueCount: 5_000,
            randomizer: randomizer);
    }

    [Params(ListFilterPolicy.CalculateDiff, ListFilterPolicy.ClearAndReplace)]
    public ListFilterPolicy FilterPolicy { get; set; }

    [Benchmark(Baseline = true)]
    public void RandomizedEditsAndStateChanges()
    {
        using var source            = new Subject<IChangeSet<Item>>();
        using var predicateState    = new Subject<int>();

        using var subscription = source
            .Filter(
                predicateState: predicateState,
                predicate:      Item.FilterByIdInclusionMask,
                filterPolicy:   FilterPolicy)
            .Subscribe();

        PublishNotifications(source, predicateState);

        subscription.Dispose();
    }

    private static ImmutableArray<IChangeSet<Item>> GenerateStressItemsAndChangeSets(
        int         editCount,
        int         maxChangeCount,
        int         maxRangeSize,
        Randomizer  randomizer)
    {
        var changeReasons = new[]
        {
            ListChangeReason.Add,
            ListChangeReason.AddRange,
            ListChangeReason.Clear,
            ListChangeReason.Moved,
            ListChangeReason.Refresh,
            ListChangeReason.Remove,
            ListChangeReason.RemoveRange,
            ListChangeReason.Replace
        };

        // Weights are chosen to make the cache size likely to grow over time,
        // exerting more pressure on the system the longer the benchmark runs.
        // Also, to prevent bogus operations (E.G. you can't remove an item from an empty cache).
        var changeReasonWeightsWhenCountIs0 = new[]
        {
            0.5f, // Add
            0.5f, // AddRange
            0.0f, // Clear
            0.0f, // Moved
            0.0f, // Refresh
            0.0f, // Remove
            0.0f, // RemoveRange
            0.0f  // Replace
        };

        var changeReasonWeightsWhenCountIs1 = new[]
        {
            0.400f,  // Add
            0.400f,  // AddRange
            0.001f,  // Clear
            0.000f,  // Moved
            0.000f,  // Refresh
            0.199f,  // Remove
            0.000f,  // RemoveRange
            0.000f   // Replace
        };

        var changeReasonWeightsOtherwise = new[]
        {
            0.250f,  // Add
            0.250f,  // AddRange
            0.001f,  // Clear
            0.100f,  // Moved
            0.099f,  // Refresh
            0.100f,  // Remove
            0.100f,  // RemoveRange
            0.100f   // Replace
        };

        var nextItemId = 1;

        var changeSets = ImmutableArray.CreateBuilder<IChangeSet<Item>>(initialCapacity: editCount);

        var items = new ChangeAwareList<Item>();

        while (changeSets.Count < changeSets.Capacity)
        {
            var changeCount = randomizer.Int(1, maxChangeCount);
            for (var i = 0; i < changeCount; ++i)
            {
                var changeReason = randomizer.WeightedRandom(changeReasons, items.Count switch
                {
                    0 => changeReasonWeightsWhenCountIs0,
                    1 => changeReasonWeightsWhenCountIs1,
                    _ => changeReasonWeightsOtherwise
                });

                switch (changeReason)
                {
                    case ListChangeReason.Add:
                        items.Add(new Item()
                        {
                            Id = nextItemId++,
                            IsIncluded = randomizer.Bool()
                        });
                        break;

                    case ListChangeReason.AddRange:
                        items.AddRange(Enumerable.Repeat(0, randomizer.Int(1, maxRangeSize))
                            .Select(_ => new Item()
                            {
                                Id = nextItemId++,
                                IsIncluded = randomizer.Bool()
                            }));
                        break;

                    case ListChangeReason.Clear:
                        items.Clear();
                        break;

                    case ListChangeReason.Moved:
                        items.Move(
                            original: randomizer.Int(0, items.Count - 1),
                            destination: randomizer.Int(0, items.Count - 1));
                        break;

                    case ListChangeReason.Refresh:
                        items.RefreshAt(randomizer.Int(0, items.Count - 1));
                        break;

                    case ListChangeReason.Remove:
                        items.RemoveAt(randomizer.Int(0, items.Count - 1));
                        break;

                    case ListChangeReason.RemoveRange:
                        {
                            var rangeStartIndex = randomizer.Int(0, items.Count - 1);

                            items.RemoveRange(
                                index: rangeStartIndex,
                                count: Math.Min(items.Count - rangeStartIndex, randomizer.Int(1, maxRangeSize)));
                        }
                        break;

                    case ListChangeReason.Replace:
                        items[randomizer.Int(0, items.Count - 1)] = new Item()
                        {
                            Id = nextItemId++,
                            IsIncluded = randomizer.Bool()
                        };
                        break;
                }
            }

            changeSets.Add(items.CaptureChanges());
        }

        return changeSets.MoveToImmutable();
    }

    private static ImmutableArray<int> GenerateRandomPredicateStates(
        int         valueCount,
        Randomizer  randomizer)
    {
        var values = ImmutableArray.CreateBuilder<int>(initialCapacity: valueCount);

        while (values.Count < valueCount)
            values.Add(randomizer.Int());

        return values.MoveToImmutable();
    }

    private void PublishNotifications(
        IObserver<IChangeSet<Item>> source,
        IObserver<int>              predicateState)
    {
        int i;
        for (i = 0; (i < _changeSets.Length) && (i < _predicateStates.Length); ++i)
        {
            source.OnNext(_changeSets[i]);
            predicateState.OnNext(_predicateStates[i]);
        }

        for (; i < _changeSets.Length; ++i)
            source.OnNext(_changeSets[i]);

        for (; i < _predicateStates.Length; ++i)
            predicateState.OnNext(_predicateStates[i]);
    }

    private readonly ImmutableArray<IChangeSet<Item>>   _changeSets;
    private readonly ImmutableArray<int>                _predicateStates;

    public class Item
    {
        public static bool FilterByIdInclusionMask(
                int     idInclusionMask,
                Item    item)
            => ((item.Id & idInclusionMask) == 0) && item.IsIncluded;
            
        public required int Id { get; init; }

        public bool IsIncluded { get; set; }

        public override string ToString()
            => $"{{ Id = {Id}, IsIncluded = {IsIncluded} }}";
    }
}
