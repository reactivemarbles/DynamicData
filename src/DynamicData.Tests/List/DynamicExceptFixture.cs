namespace DynamicData.Tests.List;

public class DynamicExceptFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<IObservable<IChangeSet<int>>> _source;

    private readonly ISourceList<int> _source1;

    private readonly ISourceList<int> _source2;

    private readonly ISourceList<int> _source3;

    public DynamicExceptFixture()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _source3 = new SourceList<int>();
        _source = new SourceList<IObservable<IChangeSet<int>>>();
        _results = _source.Except().AsAggregator();
    }

    [Test]
    public async Task AddAndRemoveLists()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _source3.AddRange(Enumerable.Range(100, 5));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        var result = Enumerable.Range(1, 5);
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        _source2.Edit(
            innerList =>
            {
                innerList.Clear();
                innerList.AddRange(Enumerable.Range(3, 5));
            });

        result = Enumerable.Range(1, 2);
        await Assert.That(_results.Data.Count).IsEqualTo(2);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        _source.RemoveAt(1);
        result = Enumerable.Range(1, 5);
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        _source.Add(_source2.Connect());
        result = Enumerable.Range(1, 2);
        await Assert.That(_results.Data.Count).IsEqualTo(2);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        //remove root except
        _source.RemoveAt(0);
        result = Enumerable.Range(100, 5);
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);
    }

    [Test]
    public async Task AddedWhenNoLongerInSecond()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        _source2.Remove(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ClearFirstClearsResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(1, 5));
        _source1.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClearSecondEnsuresFirstIsIncluded()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(1, 5));
        await Assert.That(_results.Data.Count).IsEqualTo(0);
        _source2.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 5));
    }

    [Test]
    public async Task CombineRange()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 10));
        _source2.AddRange(Enumerable.Range(6, 10));
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 5));
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
    public async Task ExcludedWhenItemIsInTwoSources()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task IncludedWhenItemIsInOneSource()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
    }

    [Test]
    public async Task NothingFromOther()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }
}
