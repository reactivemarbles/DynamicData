#if REACTIVE_TESTS
using DynamicData.Reactive.PLinq;
#else
using DynamicData.PLinq;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class FilterParallelFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public FilterParallelFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = new ChangeSetAggregator<Person, string>(_source.Connect().Filter(p => p.Age > 20, new ParallelisationOptions(ParallelType.Ordered)));
    }

    [Test]
    public async Task AddMatched()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task AddNotMatched()
    {
        var person = new Person("Adult1", 10);
        _source.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should have no item updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Cache should have no items");
    }

    [Test]
    public async Task AddNotMatchedAndUpdateMatched()
    {
        const string key = "Adult1";
        var notmatched = new Person(key, 19);
        var matched = new Person(key, 21);

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(notmatched);
                updater.AddOrUpdate(matched);
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].First().Current).IsEqualTo(matched).Because("Should be same person");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(matched).Because("Should be same person");
    }

    [Test]
    public async Task AttemptedRemovalOfANonExistentKeyWillBeIgnored()
    {
        const string key = "Adult1";
        _source.Remove(key);
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should be 0 updates");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        _source.AddOrUpdate(people);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(80).Because("Should return 80 adds");

        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Name).ToArray();
        await Assert.That(_results.Data.Items.OrderBy(p => p.Name)).IsEquivalentTo(filtered).Because("Incorrect Filter result");
    }

    [Test]
    public async Task BatchRemoves()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddOrUpdate(people);
        _source.Remove(people);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(80).Because("Should be 80 addes");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(80).Because("Should be 80 removes");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task BatchSuccessiveUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        foreach (var person in people)
        {
            var person1 = person;
            _source.AddOrUpdate(person1);
        }

        await Assert.That(_results.Messages.Count).IsEqualTo(80).Because("Should be 100 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(80).Because("Should be 100 in the cache");
        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        await Assert.That(_results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(filtered).Because("Incorrect Filter result");
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        _source.AddOrUpdate(people);
        _source.Clear();

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(80).Because("Should be 80 addes");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(80).Because("Should be 80 removes");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        _source.AddOrUpdate(person);
        _source.Remove(key);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 80 addes");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(1).Because("Should be 80 removes");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task SameKeyChanges()
    {
        const string key = "Adult1";

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(key, 50));
                updater.AddOrUpdate(new Person(key, 52));
                updater.AddOrUpdate(new Person(key, 53));
                updater.Remove(key);
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(_results.Messages[0].Updates).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Removes).IsEqualTo(1).Because("Should be 1 remove");
    }

    [Test]
    public async Task UpdateMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.AddOrUpdate(newperson);
        _source.AddOrUpdate(updated);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(_results.Messages[1].Updates).IsEqualTo(1).Because("Should be 1 update");
    }

    [Test]
    public async Task UpdateNotMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 10);
        var updated = new Person(key, 11);

        _source.AddOrUpdate(newperson);
        _source.AddOrUpdate(updated);

        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should be no updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should nothing cached");
    }
}
