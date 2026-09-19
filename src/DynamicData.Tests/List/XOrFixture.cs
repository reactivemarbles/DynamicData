namespace DynamicData.Tests.List;

[InheritsTests]
public class XOrFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable() => _source1.Connect().Xor(_source2.Connect());
}

[InheritsTests]
public class XOrCollectionFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<int>>> { _source1.Connect(), _source2.Connect() };
        return l.Xor();
    }
}

public abstract class XOrFixtureBase : IDisposable
{
    protected ISourceList<int> _source1;

    protected ISourceList<int> _source2;

    private readonly ChangeSetAggregator<int> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected XOrFixtureBase()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _results = CreateObservable().AsAggregator();
    }

    [Test]
    public async Task ClearOnlyClearsOneSource()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _source1.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(6, 5));
    }

    [Test]
    public async Task CombineRange()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        await Assert.That(_results.Data.Count).IsEqualTo(10);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 10));
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task IncludedWhenItemIsInOneSource()
    {
        _source1.Add(1);

        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(_results.Data.Items[0]).IsEqualTo(1);
    }

    [Test]
    public async Task NotIncludedWhenItemIsInTwoSources()
    {
        _source1.Add(1);
        _source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task OverlappingRangeExludesInteresct()
    {
        _source1.AddRange(Enumerable.Range(1, 10));
        _source2.AddRange(Enumerable.Range(6, 10));
        await Assert.That(_results.Data.Count).IsEqualTo(10);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 5).Union(Enumerable.Range(11, 5)));
    }

    [Test]
    public async Task RemovedWhenNoLongerInBoth()
    {
        _source1.Add(1);
        _source2.Add(1);
        _source1.Remove(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RemovedWhenNoLongerInEither()
    {
        _source1.Add(1);
        _source1.Remove(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    protected abstract IObservable<IChangeSet<int>> CreateObservable();
}
