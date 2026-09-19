using Bogus;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public static partial class DynamicPredicateState
    {
        public sealed class IntegrationTests
            : IntegrationTestFixtureBase
        {
            [Test]
            public async Task NotificationsOccurOnDifferentThreads_OperatorIsThreadSafe()
            {
                // Setup
                var randomizer = new Randomizer(0x1234567);

                (var items, var changeSets) = GenerateStressItemsAndChangeSets(
                    editCount: 5_000,
                    maxChangeCount: 20,
                    randomizer: randomizer);

                var predicateStates = GenerateRandomIdInclusionMasks(
                        valueCount: 5_000,
                        randomizer: randomizer)
                    .ToArray();

                using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();
                using var predicateState = new ReactiveUI.Primitives.Signals.Signal<int>();

                // UUT Initialization
                using var subscription = source
                    .Filter(
                        predicate: Item.FilterByIdInclusionMask,
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

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items.Items.Where(item => Item.FilterByIdInclusionMask(finalPredicateState, item))).Because("the source colleciton should be filtered to include only items matching the final predicate");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }
        }
    }
}
