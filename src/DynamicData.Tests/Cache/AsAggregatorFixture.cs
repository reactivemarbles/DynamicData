namespace DynamicData.Tests.Cache;

public class AsAggregatorFixture
{
    [Fact]
    public void DistinctAggregatorRecordsTheSourceFailure()
    {
        using var source = new Subject<IDistinctChangeSet<int>>();
        using var results = source.AsAggregator();

        var expectedError = new Exception("Test Exception");
        source.OnError(expectedError);

        results.Error.Should().BeSameAs(expectedError, "the aggregator should record the failure, not rethrow it");
    }

    [Fact]
    public void PagedAggregatorRecordsTheSourceFailure()
    {
        using var source = new Subject<IPagedChangeSet<Person, string>>();
        using var results = source.AsAggregator();

        var expectedError = new Exception("Test Exception");
        source.OnError(expectedError);

        results.Error.Should().BeSameAs(expectedError, "the aggregator should record the failure, not rethrow it");
    }

    [Fact]
    public void SortedAggregatorRecordsTheSourceFailure()
    {
        using var source = new Subject<ISortedChangeSet<Person, string>>();
        using var results = source.AsAggregator();

        var expectedError = new Exception("Test Exception");
        source.OnError(expectedError);

        results.Error.Should().BeSameAs(expectedError, "the aggregator should record the failure, not rethrow it");
    }

    [Fact]
    public void VirtualAggregatorRecordsTheSourceFailure()
    {
        using var source = new Subject<IVirtualChangeSet<Person, string>>();
        using var results = source.AsAggregator();

        var expectedError = new Exception("Test Exception");
        source.OnError(expectedError);

        results.Error.Should().BeSameAs(expectedError, "the aggregator should record the failure, not rethrow it");
    }
}
