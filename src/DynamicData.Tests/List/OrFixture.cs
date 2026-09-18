namespace DynamicData.Tests.List;

[InheritsTests]
public class OrFixture : OrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable() => _source1.Connect().Or(_source2.Connect());
}

[InheritsTests]
public class OrCollectionFixture : OrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable()
    {
        var list = new List<IObservable<IChangeSet<int>>> { _source1.Connect(), _source2.Connect() };
        return list.Or();
    }
}

public class OrRefreshFixture
{
    [Test]
    public async Task RefreshPassesThrough()
    {
        SourceList<Item> source1 = new();
        source1.Add(new Item("A"));
        SourceList<Item> source2 = new();
        source2.Add(new Item("B"));

        var list = new List<IObservable<IChangeSet<Item>>> { source1.Connect().AutoRefresh(), source2.Connect().AutoRefresh() };
        var results = list.Or().AsAggregator();
        source1.Items.ElementAt(0).Name = "Test";

        await Assert.That(results.Data.Count).IsEqualTo(2);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[2].Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages[2].First().Item.Current).IsEqualTo(source1.Items[0]);
    }
}

public class OrReplaceFixture
{
    [Test]
    public async Task ItemIsReplaced()
    {
        var item1 = new Item("A");
        var item2 = new Item("B");
        var item1Replacement = new Item("Test");

        SourceList<Item> source1 = new();
        source1.Add(item1);
        SourceList<Item> source2 = new();
        source2.Add(item2);

        var list = new List<IObservable<IChangeSet<Item>>> { source1.Connect(), source2.Connect() };
        var results = list.Or().AsAggregator();
        source1.ReplaceAt(0, item1Replacement);

        await Assert.That(results.Data.Count).IsEqualTo(2);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { item1Replacement, item2 });
    }
}

public abstract class OrFixtureBase : IDisposable
{
    protected ISourceList<int> _source1;

    protected ISourceList<int> _source2;

    private readonly ChangeSetAggregator<int> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected OrFixtureBase()
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
    public async Task IncludedWhenItemIsInTwoSources()
    {
        _source1.Add(1);
        _source2.Add(1);
        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(_results.Data.Items[0]).IsEqualTo(1);
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
