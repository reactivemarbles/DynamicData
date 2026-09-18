using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class DistinctFixture : IDisposable
{
    private readonly DistinctChangeSetAggregator<int> _results;

    private readonly ISourceCache<Person, string> _source;

    public DistinctFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = _source.Connect().DistinctValues(p => p.Age).AsAggregator();
    }

    [Test]
    public async Task BreakWithLoadsOfUpdates()
    {
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person2", 12));
                updater.AddOrUpdate(new Person("Person1", 1));
                updater.AddOrUpdate(new Person("Person1", 1));
                updater.AddOrUpdate(new Person("Person2", 12));

                updater.AddOrUpdate(new Person("Person3", 13));
                updater.AddOrUpdate(new Person("Person4", 14));
            });

        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 1, 12, 13, 14 });

        //This previously threw
        _source.Remove(new Person("Person3", 13));

        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 1, 12, 14 });
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task DuplicatedResultsResultInNoAdditionalMessage()
    {
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person1", 20));
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 update message");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 items in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(20).Because("Should 20");
    }

    [Test]
    public async Task DuplicateKeysRefreshAfterRemove()
    {
        var source1 = new SourceCache<Person, string>(p => p.Name);
        var source2 = new SourceCache<Person, string>(p => p.Name);

        var person = new Person("Person2", 12);

        var results = source1.Connect().Merge(source2.Connect()).DistinctValues(p => p.Age).AsAggregator();

        source1.AddOrUpdate(person);
        source2.AddOrUpdate(person);
        source2.Remove(person);
        source1.Refresh(person); // would previously throw KeyNotFoundException here

        await Assert.That(results.Messages).HasCount(1);
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 12 });

        source1.Remove(person);

        await Assert.That(results.Messages).HasCount(2);
        await Assert.That(results.Data.Items).IsEmpty();
    }

    [Test]
    public async Task FiresAddWhenaNewItemIsAdded()
    {
        _source.AddOrUpdate(new Person("Person1", 20));

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(20).Because("Should 20");
    }

    [Test]
    public async Task FiresBatchResultOnce()
    {
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("Should be 3 items in the cache");

        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 20, 21, 22 });
        await Assert.That(_results.Data.Items[0]).IsEqualTo(20).Because("Should 20");
    }

    [Test]
    public async Task RemovingAnItemRemovesTheDistinct()
    {
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.Remove(new Person("Person1", 20));
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 1 update message");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 1 items in the cache");

        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1).Because("First message should be an add");
        await Assert.That(_results.Messages.Skip(1).First().Removes).IsEqualTo(1).Because("Second messsage should be a remove");
    }
}
