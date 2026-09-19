namespace DynamicData.Tests.List;

[InheritsTests]
public class ExceptFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable() => Source1.Connect().Except(Source2.Connect());
}

[InheritsTests]
public class ExceptCollectionFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<int>>> { Source1.Connect(), Source2.Connect() };
        return l.Except();
    }
}

public abstract class ExceptFixtureBase : IDisposable
{
    protected ISourceList<int> Source1;

    protected ISourceList<int> Source2;

    private readonly ChangeSetAggregator<int> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected ExceptFixtureBase()
    {
        Source1 = new SourceList<int>();
        Source2 = new SourceList<int>();
        _results = CreateObservable().AsAggregator();
    }

    [Test]
    public async Task AddedWhenNoLongerInSecond()
    {
        Source1.Add(1);
        Source2.Add(1);
        Source2.Remove(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ClearFirstClearsResult()
    {
        Source1.AddRange(Enumerable.Range(1, 5));
        Source2.AddRange(Enumerable.Range(1, 5));
        Source1.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ClearSecondEnsuresFirstIsIncluded()
    {
        Source1.AddRange(Enumerable.Range(1, 5));
        Source2.AddRange(Enumerable.Range(1, 5));
        await Assert.That(_results.Data.Count).IsEqualTo(0);
        Source2.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 5));
    }

    [Test]
    public async Task CombineRange()
    {
        Source1.AddRange(Enumerable.Range(1, 10));
        Source2.AddRange(Enumerable.Range(6, 10));
        await Assert.That(_results.Data.Count).IsEqualTo(5);
        await Assert.That(_results.Data.Items).IsEquivalentTo(Enumerable.Range(1, 5));
    }

    public void Dispose()
    {
        Source1.Dispose();
        Source2.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task ExcludedWhenItemIsInTwoSources()
    {
        Source1.Add(1);
        Source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task IncludedWhenItemIsInOneSource()
    {
        Source1.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
    }

    [Test]
    public async Task NothingFromOther()
    {
        Source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    protected abstract IObservable<IChangeSet<int>> CreateObservable();
}
