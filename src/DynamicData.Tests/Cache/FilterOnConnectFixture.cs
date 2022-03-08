using System.Linq;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// See https://github.com/reactivemarbles/DynamicData/issues/400
/// </summary>
public class FilterOnConnectFixture
{
    [Fact]
    public void ClearingSourceCacheWithPredicateShouldClearTheData()
    {
        // having
        var source = new SourceCache<int, int>(it => it);
        source.AddOrUpdate(1);
        var results = source.Connect(it => true).AsAggregator();

        // when
        source.Clear();

        // then
        results.Data.Count.Should().Be(0, "Should be 0");
    }

    [Fact]
    public void UpdatesExistedBeforeConnectWithoutPredicateShouldBeVisibleAsPreviousWhenNewUpdatesTriggered()
    {
        // having
        var source = new SourceCache<int, int>(it => it);
        source.AddOrUpdate(1);
        var results = source.Connect().AsAggregator();

        // when
        source.AddOrUpdate(1);

        // then
        results.Messages.Count.Should().Be(2, "Should be 2 updates");
        results.Messages[1].First().Previous.HasValue.Should().Be(true, "Should have previous value");
    }
}
