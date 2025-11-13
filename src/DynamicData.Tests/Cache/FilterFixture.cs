using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

using Bogus;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public enum CompletionStrategy
    {
        Immediate,
        Asynchronous
    }

    public enum EmptyChangesetPolicy
    {
        SuppressEmptyChangesets,
        IncludeEmptyChangesets
    }

    public enum DynamicParameter
    {
        Source,
        PredicateChanged,
        ReapplyFilter
    }

    public record Item
    {
        public static bool FilterByEvenId(Item item)
            => (item.Id % 2) == 0;

        public static bool FilterByIsIncluded(Item item)
            => item.IsIncluded;

        public static bool FilterByIdInclusionMask(
                int     idInclusionMask,
                Item    item)
            => ((item.Id & idInclusionMask) == 0) && item.IsIncluded;

        public static int SelectId(Item item)
            => item.Id;
            
        public required int Id { get; init; }

        public bool IsIncluded { get; set; }
    }

    private static (ICache<Item, int> items, IReadOnlyList<IChangeSet<Item, int>> changeSets) GenerateStressItemsAndChangeSets(
        int         editCount,
        int         maxChangeCount,
        Randomizer  randomizer)
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

        var nextItemId = 1;

        var changeSets = new List<IChangeSet<Item, int>>(capacity: editCount);

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

        return (items, changeSets);
    }

    private static IReadOnlyList<int> GenerateRandomIdInclusionMasks(
        int         valueCount,
        Randomizer  randomizer)
    {
        var values = new List<int>(capacity: valueCount);

        while (values.Count < valueCount)
            values.Add(randomizer.Int());

        return values;
    }
}
