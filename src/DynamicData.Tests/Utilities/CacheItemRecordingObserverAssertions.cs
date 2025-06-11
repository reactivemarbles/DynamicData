using FluentAssertions;

namespace DynamicData.Tests.Utilities;

public static class CacheItemRecordingObserverAssertions
{
    public static void ShouldNotSupportSorting<TObject, TKey>(
            this    CacheItemRecordingObserver<TObject, TKey>   results,
                    string                                      because = "")
        where TObject   : notnull
        where TKey      : notnull
    {
        results.RecordedChangeSets.Should().AllSatisfy(changeSet =>
        {
            if (changeSet.Count is not 0)
                changeSet.Should().AllSatisfy(change =>
                {
                    change.CurrentIndex.Should().Be(-1, because);
                    change.PreviousIndex.Should().Be(-1, because);
                });
        });
        results.RecordedItemsSorted.Should().BeEmpty(because);
    }
}
