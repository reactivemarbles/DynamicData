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
    public static partial class DynamicPredicateState
    {
        public sealed class IntegrationTests
            : IntegrationTestFixtureBase
        {
            [Fact]
            public async Task NotificationsOccurOnDifferentThreads_OperatorIsThreadSafe()
            {
                // Setup
                var randomizer = new Randomizer(0x1234567);

                (var items, var changeSets) = GenerateStressItemsAndChangeSets(
                    editCount:      5_000,
                    maxChangeCount: 20,
                    randomizer:     randomizer);
            
                var predicateStates = GenerateRandomIdInclusionMasks(
                        valueCount: 5_000,
                        randomizer: randomizer)
                    .ToArray();

                using var source            = new Subject<IChangeSet<Item, int>>();
                using var predicateState    = new Subject<int>();


                // UUT Initialization
                using var subscription = source
                    .Filter(
                        predicate:      Item.FilterByIdInclusionMask,
                        predicateState: predicateState)
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
                        foreach (var value in predicateStates)
                            predicateState.OnNext(value);
                    }));

                var finalPredicateState = predicateStates[^1];

                results.Error.Should().BeNull();
                results.RecordedItemsByKey.Values.Should().BeEquivalentTo(items.Items.Where(item => Item.FilterByIdInclusionMask(finalPredicateState, item)), "the source colleciton should be filtered to include only items matching the final predicate");
                results.HasCompleted.Should().BeFalse("the source has not completed");


                // Final verification
                results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }
        }
    }
}
