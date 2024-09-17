using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;

using BenchmarkDotNet.Attributes;

using Bogus;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class Filter_Cache_WithPredicateState
{
    public Filter_Cache_WithPredicateState()
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

        var randomizer = new Randomizer(0x1234567);

        var changeSets = ImmutableArray.CreateBuilder<IChangeSet<Item, int>>(initialCapacity: 5_000);
        var nextItemId = 1;
        var items = new ChangeAwareCache<Item, int>();
        while (changeSets.Count < changeSets.Capacity)
        {
            var changeCount = randomizer.Int(1, maxChangeCount);
            for (var i = 0; i < changeCount; ++i)
            {
                var changeReason = randomizer.WeightedRandom(changeReasons, items.Count switch
                {
                    0   => changeReasonWeightsWhenCountIs0,
                    _   => changeReasonWeightsOtherwise
                });

                switch (changeReason)
                {
                    case ChangeReason.Add:
                        items.AddOrUpdate(
                            item:   new Item()
                            {
                                Id          = nextItemId,
                                IsIncluded  = randomizer.Bool()
                            },
                            key:    nextItemId);
                        ++nextItemId;
                        break;

                    case ChangeReason.Refresh:
                        items.Refresh(items.Keys.ElementAt(randomizer.Int(0, items.Count - 1)));
                        break;

                    case ChangeReason.Remove:
                        items.Remove(items.Keys.ElementAt(randomizer.Int(0, items.Count - 1)));
                        break;

                    case ChangeReason.Update:
                        var id = items.Keys.ElementAt(randomizer.Int(0, items.Count - 1));
                        items.AddOrUpdate(
                            item:   new Item()
                            {
                                Id          = id,
                                IsIncluded  = randomizer.Bool()
                            },
                            key:    id);
                        break;
                }
            }

            changeSets.Add(items.CaptureChanges());
        }
        _changeSets = changeSets.MoveToImmutable();


        var predicateStates = ImmutableArray.CreateBuilder<int>(initialCapacity: 5_000);
        while (predicateStates.Count < predicateStates.Capacity)
            predicateStates.Add(randomizer.Int());
        _predicateStates = predicateStates.MoveToImmutable();
    }

    [Benchmark(Baseline = true)]
    public void RandomizedEditsAndStateChanges()
    {
        using var source            = new Subject<IChangeSet<Item, int>>();
        using var predicateState    = new Subject<int>();

        using var subscription = source
            .Filter(
                predicateState: predicateState,
                predicate:      Item.FilterByIdInclusionMask)
            .Subscribe();

        PublishNotifications(source, predicateState);

        subscription.Dispose();
    }

    private void PublishNotifications(
        IObserver<IChangeSet<Item, int>>    source,
        IObserver<int>                      predicateState)
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

    private readonly ImmutableArray<IChangeSet<Item, int>>  _changeSets;
    private readonly ImmutableArray<int>                    _predicateStates;

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
