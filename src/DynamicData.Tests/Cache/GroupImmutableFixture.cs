#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class GroupImmutableFixture : IDisposable
{
    private readonly ChangeSetAggregator<IGrouping<Person, string, int>, int> _results;

    private readonly ISourceCache<Person, string> _source;

    public GroupImmutableFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = _source.Connect().GroupWithImmutableState(p => p.Age).AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        _source.AddOrUpdate(new Person("Person1", 20));
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 add");
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
    }

    [Test]
    public async Task ChanegMultipleGroups()
    {
        var initialPeople = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i % 10)).ToArray();

        _source.AddOrUpdate(initialPeople);

        foreach (var group in initialPeople.GroupBy(p => p.Age))
        {
            var cache = _results.Data.Lookup(group.Key).Value;
            await Assert.That(cache.Items).IsEquivalentTo(group);
        }

        var changedPeople = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i % 5)).ToArray();

        _source.AddOrUpdate(changedPeople);

        foreach (var group in changedPeople.GroupBy(p => p.Age))
        {
            var cache = _results.Data.Lookup(group.Key).Value;
            await Assert.That(cache.Items).IsEquivalentTo(group);
        }

        await Assert.That(_results.Messages.Count).IsEqualTo(2);
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(10);
        await Assert.That(_results.Messages.Skip(1).First().Removes).IsEqualTo(5);
        await Assert.That(_results.Messages.Skip(1).First().Updates).IsEqualTo(5);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task FiresManyValueForBatchOfDifferentAdds()
    {
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
                updater.AddOrUpdate(new Person("Person4", 23));
            });

        await Assert.That(_results.Data.Count).IsEqualTo(4);
        await Assert.That(_results.Messages.Count).IsEqualTo(1);
        await Assert.That(_results.Messages.First().Count).IsEqualTo(4);
        foreach (var update in _results.Messages.First())
        {
            await Assert.That(update.Reason).IsEqualTo(ChangeReason.Add);
        }
    }

    [Test]
    public async Task FiresOnlyOnceForABatchOfUniqueValues()
    {
        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 20));
                updater.AddOrUpdate(new Person("Person3", 20));
                updater.AddOrUpdate(new Person("Person4", 20));
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1);
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(_results.Data.Items[0].Count).IsEqualTo(4);
    }

    [Test]
    public async Task Reevaluate()
    {
        var initialPeople = Enumerable.Range(1, 10).Select(i => new Person("Person" + i, i % 2)).ToArray();

        _source.AddOrUpdate(initialPeople);
        await Assert.That(_results.Messages.Count).IsEqualTo(1);

        //do an inline update
        foreach (var person in initialPeople)
        {
            person.Age += 1;
        }

        //signal operators to evaluate again
        _source.Refresh();

        foreach (var group in initialPeople.GroupBy(p => p.Age))
        {
            var cache = _results.Data.Lookup(group.Key).Value;
            await Assert.That(cache.Items).IsEquivalentTo(group);
        }

        await Assert.That(_results.Data.Count).IsEqualTo(2);
        await Assert.That(_results.Messages.Count).IsEqualTo(2);

        var secondMessage = _results.Messages.Skip(1).First();
        await Assert.That(secondMessage.Removes).IsEqualTo(1);
        await Assert.That(secondMessage.Updates).IsEqualTo(1);
        await Assert.That(secondMessage.Adds).IsEqualTo(1);
    }

    [Test]
    public async Task Remove()
    {
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.Remove(new Person("Person1", 20));

        await Assert.That(_results.Messages.Count).IsEqualTo(2);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task UpdateAnItemWillChangedThegroup()
    {
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.AddOrUpdate(new Person("Person1", 21));

        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(_results.Messages.Skip(1).First().Adds).IsEqualTo(1);
        await Assert.That(_results.Messages.Skip(1).First().Removes).IsEqualTo(1);
        var group = _results.Data.Items[0];
        await Assert.That(group.Count).IsEqualTo(1);

        await Assert.That(group.Key).IsEqualTo(21);
    }

    [Test]
    public async Task UpdatesArePermissible()
    {
        _source.AddOrUpdate(new Person("Person1", 20));
        _source.AddOrUpdate(new Person("Person2", 20));

        await Assert.That(_results.Data.Count).IsEqualTo(1); //1 group
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(_results.Messages.Skip(1).First().Updates).IsEqualTo(1);

        var group = _results.Data.Items[0];
        await Assert.That(group.Count).IsEqualTo(2);
    }
}
