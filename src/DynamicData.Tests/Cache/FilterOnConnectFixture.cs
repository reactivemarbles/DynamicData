namespace DynamicData.Tests.Cache;

/// <summary>
/// See https://github.com/reactivemarbles/DynamicData/issues/400
/// </summary>
public class FilterOnConnectFixture
{
    [Test]
    public async Task ClearingSourceCacheWithPredicateShouldClearTheData()
    {
        // having
        var source = new SourceCache<int, int>(it => it);
        source.AddOrUpdate(1);
        var results = source.Connect(it => true).AsAggregator();

        // when
        source.Clear();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(0).Because("Should be 0");
    }

    [Test]
    public async Task UpdatesExistedBeforeConnectWithoutPredicateShouldBeVisibleAsPreviousWhenNewUpdatesTriggered()
    {
        // having
        var source = new SourceCache<int, int>(it => it);
        source.AddOrUpdate(1);
        var results = source.Connect().AsAggregator();

        // when
        source.AddOrUpdate(1);

        // then
        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(results.Messages[1].First().Previous.HasValue).IsTrue().Because("Should have previous value");
    }
}
