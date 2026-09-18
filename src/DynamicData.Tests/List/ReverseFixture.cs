namespace DynamicData.Tests.List;

public class ReverseFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<int> _source;

    public ReverseFixture()
    {
        _source = new SourceList<int>();
        _results = _source.Connect().Reverse().AsAggregator();
    }

    [Test]
    public async Task AddInSucession()
    {
        _source.Add(1);
        _source.Add(2);
        _source.Add(3);
        _source.Add(4);
        _source.Add(5);

        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 5, 4, 3, 2, 1 });
    }

    [Test]
    public async Task AddRange()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 5, 4, 3, 2, 1 });
    }

    [Test]
    public async Task Clear()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        _source.Clear();
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task Move()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        _source.Move(4, 1);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 4, 3, 2, 5, 1 });
    }

    [Test]
    public async Task Move2()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        _source.Move(1, 4);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 2, 5, 4, 3, 1 });
    }

    [Test]
    public async Task RemoveRange()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        _source.RemoveRange(1, 3);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 5, 1 });
    }

    [Test]
    public async Task RemoveRangeThenInsert()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        _source.RemoveRange(1, 3);
        _source.Insert(1, 3);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 5, 3, 1 });
    }

    [Test]
    public async Task Removes()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        _source.Remove(1);
        _source.Remove(4);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 5, 3, 2 });
    }

    [Test]
    public async Task Replace()
    {
        _source.AddRange(Enumerable.Range(1, 5));
        _source.ReplaceAt(2, 100);
        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 5, 4, 100, 2, 1 });
    }
}
