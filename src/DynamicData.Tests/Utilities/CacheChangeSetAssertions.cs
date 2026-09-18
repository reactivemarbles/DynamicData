namespace DynamicData.Tests.Utilities;

public static class CacheChangeSetAssertions
{
    public static async Task ShouldHaveRefreshed<TObject, TKey>(
            this IChangeSet<TObject, TKey> changeSet,
                    IEnumerable<TObject> expectedItems,
                    string because = "")
        where TObject : notnull
        where TKey : notnull
        => await Assert.That(changeSet
                .Where(static change => change.Reason is ChangeReason.Refresh)
                .Select(static change => change.Current))
            .IsEquivalentTo(expectedItems, TUnit.Assertions.Enums.CollectionOrdering.Any);
}
