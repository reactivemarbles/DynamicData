using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using Bogus;
using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public static partial class DynamicPredicate
    {
        public sealed class IntegrationTests
            : IntegrationTestFixtureBase
        {
            [Fact(Timeout = 60_000)]
            public async Task NotificationsOccurOnDifferentThreads_OperatorIsThreadSafe()
            {
                // Setup
                var randomizer = new Randomizer(0x1234567);

                (var items, var changeSets) = GenerateStressItemsAndChangeSets(
                    editCount:      5_000,
                    maxChangeCount: 20,
                    randomizer:     randomizer);
            
                var predicates = GenerateRandomIdInclusionMasks(
                        valueCount: 5_000,
                        randomizer: randomizer)
                    .Select(mask => new Func<Item, bool>(item => Item.FilterByIdInclusionMask(mask, item)))
                    .ToArray();

                using var source            = new Subject<IChangeSet<Item, int>>();
                using var predicateChanged  = new Subject<Func<Item, bool>>();


                // UUT Initialization
                using var subscription = source
                    .Filter(predicateChanged)
                    .ValidateSynchronization()
                    .ValidateChangeSets(Item.SelectId)
                    .RecordCacheItems(out var results);


                // UUT Action
                await Task.WhenAll(
                    Task.Run(() =>
                    {
                        foreach (var changeSet in changeSets)
                            source.OnNext(changeSet);
                    }),
                    Task.Run(() =>
                    {
                        foreach (var predicate in predicates)
                            predicateChanged.OnNext(predicate);
                    }));

                var finalPredicate = predicates[^1];

                results.Error.Should().BeNull();
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items.Items.Where(finalPredicate), "the source colleciton should be filtered to include only items matching the final predicate");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }
        }
    }
}
