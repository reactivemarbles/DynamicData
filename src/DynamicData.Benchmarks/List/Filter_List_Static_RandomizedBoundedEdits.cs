using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using BenchmarkDotNet.Attributes;

using Bogus;

namespace DynamicData.Benchmarks.List;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class Filter_List_Static_RandomizedBoundedEdits
{
    public Filter_List_Static_RandomizedBoundedEdits()
    {
        var randomizer = new Randomizer(0x1234567);

        _changeSetsByInitialItemCount = GetType()
            .GetProperty(nameof(InitialItemCount))!
            .GetCustomAttribute<ParamsAttribute>()!
            .Values
            .Cast<int>()
            .ToDictionary(
                keySelector:        static initialItemCount => initialItemCount,
                elementSelector:    initialItemCount => GenerateChangeSets(
                    initialItemCount:   initialItemCount,
                    editCount:          5_000,
                    maxChangeCount:     20,
                    maxRangeSize:       10,
                    randomizer:         randomizer));
    }

    [Params(
        0,
        10,
        100,
        1_000,
        10_000)]
    public int InitialItemCount { get; set; }

    [Benchmark(Baseline = true)]
    public void CurrentImplementation()
    {
        using var source = new Subject<IChangeSet<Item>>();

        using var subscription = source
            .Filter(Item.FilterByIsIncluded)
            .Subscribe();

        Run(source);
    }

    private static ImmutableArray<IChangeSet<Item>> GenerateChangeSets(
        int         initialItemCount,
        int         editCount,
        int         maxChangeCount,
        int         maxRangeSize,
        Randomizer  randomizer)
    {
        var changeReasons = new[]
        {
            ListChangeReason.Add,
            ListChangeReason.AddRange,
            ListChangeReason.Moved,
            ListChangeReason.Refresh,
            ListChangeReason.Remove,
            ListChangeReason.RemoveRange,
            ListChangeReason.Replace
        };

        // Weights are chosen to make the cache size likely to stay the same, over time.
        // Also, to prevent bogus operations (E.G. you can't remove an item from an empty cache).
        var changeReasonWeightsWhenCountIs0 = new[]
        {
            0.5f,   // Add
            0.5f,   // AddRange
            0.0f,   // Moved
            0.0f,   // Refresh
            0.0f,   // Remove
            0.0f,   // RemoveRange
            0.0f    // Replace
        };

        var changeReasonWeightsWhenCountIs1 = new[]
        {
            0.25f,  // Add
            0.25f,  // AddRange
            0.00f,  // Moved
            0.00f,  // Refresh
            0.50f,  // Remove
            0.00f,  // RemoveRange
            0.00f   // Replace
        };

        var changeReasonWeightsOtherwise = new[]
        {
            0.15f,  // Add
            0.15f,  // AddRange
            0.15f,  // Moved
            0.10f,  // Refresh
            0.15f,  // Remove
            0.15f,  // RemoveRange
            0.15f   // Replace
        };

        var nextItemId = 1;

        var changeSets = ImmutableArray.CreateBuilder<IChangeSet<Item>>(initialCapacity: editCount);

        var items = new ChangeAwareList<Item>();
        
        items.AddRange(Enumerable.Repeat(0, initialItemCount)
            .Select(_ => new Item()
            {
                Id = nextItemId++,
                IsIncluded = randomizer.Bool()
            })
            .ToArray());
        changeSets.Add(items.CaptureChanges());
        
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

    private void Run(IObserver<IChangeSet<Item>> source)
    {
        foreach (var changeSet in _changeSetsByInitialItemCount[InitialItemCount])
            source.OnNext(changeSet);
    }

    private readonly Dictionary<int, ImmutableArray<IChangeSet<Item>>> _changeSetsByInitialItemCount;

    public class Item
    {
        public static bool FilterByIsIncluded(Item item)
            => item.IsIncluded;
            
        public required int Id { get; init; }

        public bool IsIncluded { get; set; }

        public override string ToString()
            => $"{{ Id = {Id}, IsIncluded = {IsIncluded} }}";
    }
}
