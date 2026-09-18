namespace DynamicData.Tests.Utilities;

public static class CacheItemRecordingObserverAssertions
{
    public static async Task ShouldNotSupportSorting<TObject, TKey>(
            this CacheItemRecordingObserver<TObject, TKey> results,
                    string because = "")
        where TObject : notnull
        where TKey : notnull
    {
        foreach (var changeSet in results.RecordedChangeSets)
        {
            if (changeSet.Count is not 0)
            {
                foreach (var change in changeSet)
                {
                    await Assert.That(change.CurrentIndex).IsEqualTo(-1);
                    await Assert.That(change.PreviousIndex).IsEqualTo(-1);
                }
            }
        }

        await Assert.That(results.RecordedItemsSorted).IsEmpty();
    }
}
