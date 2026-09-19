using Bogus;

namespace DynamicData.Tests.Cache;

public static partial class FilterFixture
{
    public static partial class DynamicPredicate
    {
        public sealed class IntegrationTests
            : IntegrationTestFixtureBase
        {
            [Test, Timeout(60_000)]
            public async Task NotificationsOccurOnDifferentThreads_OperatorIsThreadSafe(CancellationToken cancellationToken)
            {
                // Setup
                var randomizer = new Randomizer(0x1234567);

                (var items, var changeSets) = GenerateStressItemsAndChangeSets(
                    editCount: 5_000,
                    maxChangeCount: 20,
                    randomizer: randomizer);

                var predicates = GenerateRandomIdInclusionMasks(
                        valueCount: 5_000,
                        randomizer: randomizer)
                    .Select(mask => new Func<Item, bool>(item => Item.FilterByIdInclusionMask(mask, item)))
                    .ToArray();

                using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();
                using var predicateChanged = new ReactiveUI.Primitives.Signals.Signal<Func<Item, bool>>();

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
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            source.OnNext(changeSet);
                        }
                    }, cancellationToken),
                    Task.Run(() =>
                    {
                        foreach (var predicate in predicates)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            predicateChanged.OnNext(predicate);
                        }
                    }, cancellationToken));

                var finalPredicate = predicates[^1];

                await Assert.That(results.Error).IsNull();
                await Assert.That(results.RecordedItemsByKey.Values).IsEquivalentTo(items.Items.Where(finalPredicate)).Because("the source colleciton should be filtered to include only items matching the final predicate");
                await Assert.That(results.HasCompleted).IsFalse().Because("the source has not completed");

                // Final verification
                await results.ShouldNotSupportSorting("sorting is not supported by filter operators");
            }
        }
    }
}
