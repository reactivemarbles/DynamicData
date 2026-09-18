namespace DynamicData.Tests.Utilities;

public static class ListChangeSetAssertions
{
    public static async Task ShouldHaveRefreshed<T>(
                this IChangeSet<T> changeSet,
                        IEnumerable<T> expectedItems,
                        string because = "")
            where T : notnull
        => await Assert.That(changeSet
                .Where(static change => change.Reason is ListChangeReason.Refresh)
                .Select(static change => change.Item.Current))
            .IsEquivalentTo(expectedItems, TUnit.Assertions.Enums.CollectionOrdering.Any);
}
