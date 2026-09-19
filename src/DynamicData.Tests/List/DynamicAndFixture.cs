namespace DynamicData.Tests.List;

public class DynamicAndFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<IObservable<IChangeSet<int>>> _source;

    private readonly ISourceList<int> _source1;

    private readonly ISourceList<int> _source2;

    private readonly ISourceList<int> _source3;

    public DynamicAndFixture()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _source3 = new SourceList<int>();
        _source = new SourceList<IObservable<IChangeSet<int>>>();
        _results = _source.And().AsAggregator();
    }

    [Test]
    public async Task AddAndRemoveLists()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source3.AddRange(Enumerable.Range(1, 5));

        _source.Add(_source1.Connect());
        _source.Add(_source3.Connect());

        var result = Enumerable.Range(1, 5).ToArray();

        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        _source2.AddRange(Enumerable.Range(6, 5));
        await Assert.That(_results.Data.Count).IsEqualTo(5);

        _source.Add(_source2.Connect());
        await Assert.That(_results.Data.Count).IsEqualTo(0);

        _source.RemoveAt(2);
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);
    }

    [Test]
    public async Task ClearOneClearsResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(1, 5));
        _source1.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CombineRange()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 10));
        _source2.AddRange(Enumerable.Range(6, 10));
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(6, 5));
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
    public async Task ExcludedWhenItemIsInOneSource()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task IncludedWhenItemIsInTwoSources()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RemovedWhenNoLongerInBoth()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        _source1.Remove(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }
}
