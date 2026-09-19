namespace DynamicData.Tests.List;

public class DynamicXOrFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<IObservable<IChangeSet<int>>> _source;

    private readonly ISourceList<int> _source1;

    private readonly ISourceList<int> _source2;

    private readonly ISourceList<int> _source3;

    public DynamicXOrFixture()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _source3 = new SourceList<int>();
        _source = new SourceList<IObservable<IChangeSet<int>>>();
        _results = _source.Xor().AsAggregator();
    }

    [Test]
    public async Task AddAndRemoveLists()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _source3.AddRange(Enumerable.Range(1, 5));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        var result = Enumerable.Range(6, 5);
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        _source.RemoveAt(0);
        result = Enumerable.Range(1, 5).Union(Enumerable.Range(6, 5));
        await Assert.That(_results.Data.Count).IsEqualTo(10);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        _source.Add(_source1.Connect());
        result = Enumerable.Range(6, 5);
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);
    }

    [Test]
    public async Task ClearOnlyClearsOneSource()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _source1.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(6, 5));
    }

    [Test]
    public async Task CombineRange()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        await Assert.That(_results.Data.Count).IsEqualTo(10);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 10));
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _source3.Dispose();
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task IncludedWhenItemIsInOneSource()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);

        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(_results.Data.Items[0]).IsEqualTo(1);
    }

    [Test]
    public async Task NotIncludedWhenItemIsInTwoSources()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OverlappingRangeExcludesIntersect()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 10));
        _source2.AddRange(Enumerable.Range(6, 10));

        await Assert.That(_results.Data.Count).IsEqualTo(10);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 5).Union(Enumerable.Range(11, 5)));
    }

    [Test]
    public async Task RemovedWhenNoLongerInBoth()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        _source1.Remove(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RemovedWhenNoLongerInEither()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source1.Remove(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }
}
