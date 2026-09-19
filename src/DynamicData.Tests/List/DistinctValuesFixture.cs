using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class DistinctValuesFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<Person> _source;

    public DistinctValuesFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().DistinctValues(p => p.Age).AsAggregator();
    }

    [Test]
    public async Task AddingRemovedItem()
    {
        var person = new Person("A", 20);

        _source.Add(person);
        _source.Remove(person);
        _source.Add(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(3).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");

        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 20 });
        await Assert.That(_results.Messages.ElementAt(0).Adds).IsEqualTo(1).Because("First message should be an add");
        await Assert.That(_results.Messages.ElementAt(1).Removes).IsEqualTo(1).Because("Second message should be a remove");
        await Assert.That(_results.Messages.ElementAt(2).Adds).IsEqualTo(1).Because("Third message should be an add");
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
            list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person1", 20));
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 update message");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 items in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(20).Because("Should 20");
    }

    [Test]
    public async Task FiresAddWhenaNewItemIsAdded()
    {
        _source.Add(new Person("Person1", 20));

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(20).Because("Should 20");
    }

    [Test]
    public async Task FiresBatchResultOnce()
    {
        _source.Edit(
            list =>
            {
                list.Add(new Person("Person1", 20));
                list.Add(new Person("Person2", 21));
                list.Add(new Person("Person3", 22));
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("Should be 3 items in the cache");

        await Assert.That(_results.Data.Items).IsEquivalentTo(new[] { 20, 21, 22 });
        await Assert.That(_results.Data.Items[0]).IsEqualTo(20).Because("Should 20");
    }

    [Test]
    public async Task RemovingAnItemRemovesTheDistinct()
    {
        var person = new Person("Person1", 20);

        _source.Add(person);
        _source.Remove(person);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 1 update message");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 1 items in the cache");

        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1).Because("First message should be an add");
        await Assert.That(_results.Messages.Skip(1).First().Removes).IsEqualTo(1).Because("Second messsage should be a remove");
    }

    [Test]
    public async Task Replacing()
    {
        var person = new Person("A", 20);
        var replaceWith = new Person("A", 21);

        _source.Add(person);
        _source.Replace(person, replaceWith);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 1 update message");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 items in the cache");

        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1).Because("First message should be an add");
        await Assert.That(_results.Messages.Skip(1).First().Count).IsEqualTo(2).Because("Second messsage should be an add an a remove");
    }
}
