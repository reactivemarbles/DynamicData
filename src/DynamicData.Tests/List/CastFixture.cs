namespace DynamicData.Tests.List;

public class CastFixture : IDisposable
{
    private readonly ChangeSetAggregator<decimal> _results;

    private readonly ISourceList<int> _source;

    public CastFixture()
    {
        _source = new SourceList<int>();
        _results = _source.Cast(i => (decimal)i).AsAggregator();
    }

    [Test]
    public async Task CanCast()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        await Assert.That(_results.Data.Count).IsEqualTo(10);

        _source.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
