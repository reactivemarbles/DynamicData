namespace DynamicData.Tests.List;

public class DynamicOrRefreshFixture
{
    [Test]
    public async Task RefreshPassesThrough()
    {
        var source1 = new SourceList<Item>();
        var source2 = new SourceList<Item>();
        var source = new SourceList<IObservable<IChangeSet<Item>>>();
        var results = source.Or().AsAggregator();

        source1.Add(new Item("A"));
        source2.Add(new Item("B"));
        source.AddRange(new[] { source1.Connect().AutoRefresh(), source2.Connect().AutoRefresh() });

        source1.Items.ElementAt(0).Name = "Test";

        await Assert.That(results.Data.Count).IsEqualTo(2);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[2].Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages[2].First().Item.Current).IsEqualTo(source1.Items[0]);
    }
}

public class DynamicOrFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<IObservable<IChangeSet<int>>> _source;

    private readonly ISourceList<int> _source1;

    private readonly ISourceList<int> _source2;

    private readonly ISourceList<int> _source3;

    public DynamicOrFixture()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _source3 = new SourceList<int>();
        _source = new SourceList<IObservable<IChangeSet<int>>>();
        _results = _source.Or().AsAggregator();
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

        var result = Enumerable.Range(1, 5).Union(Enumerable.Range(6, 5)).Union(Enumerable.Range(100, 5));

        await Assert.That(_results.Data.Count).IsEqualTo(15);
        await Assert.That(_results.Data.Items).IsEquivalentTo(result);

        _source.RemoveAt(1);
        await Assert.That(_results.Data.Count).IsEqualTo(10);

        result = Enumerable.Range(1, 5).Union(Enumerable.Range(100, 5));
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
    public async Task ClearSource()
    {
        _source1.Add(0);
        _source2.Add(1);
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Clear();

        await Assert.That(_results.Data.Count).IsEqualTo(0);
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
    public async Task IncludedWhenItemIsInTwoSources()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(_results.Data.Items[0]).IsEqualTo(1);
    }

    [Test]
    public async Task ItemIsReplaced()
    {
        _source1.Add(0);
        _source2.Add(1);
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.ReplaceAt(0, 9);

        await Assert.That(_results.Data.Count).IsEqualTo(2);
        await Assert.That(_results.Messages.Count).IsEqualTo(3);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 9, 1 });
    }

    [Test]
    public async Task RemoveAllLists()
    {
        _source1.AddRange(Enumerable.Range(1, 5));

        _source3.AddRange(Enumerable.Range(100, 5));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        _source2.AddRange(Enumerable.Range(6, 5));
        _source.Clear();

        await Assert.That(_results.Data.Count).IsEqualTo(0);
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
