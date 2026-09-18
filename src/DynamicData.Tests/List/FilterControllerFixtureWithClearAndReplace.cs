using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class FilterControllerFixtureWithClearAndReplace : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.ISignal<Func<Person, bool>> _filter;

    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public FilterControllerFixtureWithClearAndReplace()
    {
        _source = new SourceList<Person>();
        _filter = new ReactiveUI.Primitives.Signals.StateSignal<Func<Person, bool>>(p => p.Age > 20);
        _results = _source.Connect().Filter(_filter, ListFilterPolicy.ClearAndReplace).AsAggregator();
    }

    /* Should be the same as standard lambda filter */

    [Test]
    public async Task AddMatched()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task AddNotMatched()
    {
        var person = new Person("Adult1", 10);
        _source.Add(person);

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
                updater.Add(notmatched);
                updater.Add(matched);
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].First().Range.First()).IsEqualTo(matched).Because("Should be same person");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(matched).Because("Should be same person");
    }

    [Test]
    public async Task AttemptedRemovalOfANonExistentKeyWillBeIgnored()
    {
        _source.Remove(new Person("A", 1));
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should be 0 updates");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        _source.AddRange(people);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(80).Because("Should return 80 adds");

        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        await Assert.That(_results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(filtered).Because("Incorrect Filter result");
    }

    [Test]
    public async Task BatchRemoves()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddRange(people);
        _source.Clear();

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
            _source.Add(person1);
        }

        await Assert.That(_results.Messages.Count).IsEqualTo(80).Because("Should be 80 messages");
        await Assert.That(_results.Data.Count).IsEqualTo(80).Because("Should be 80 in the cache");
        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        await Assert.That(_results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(filtered).Because("Incorrect Filter result");
    }

    [Test]
    public async Task ChangeFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToList();

        _source.AddRange(people);
        await Assert.That(_results.Data.Count).IsEqualTo(80).Because("Should be 80 people in the cache");

        _filter.OnNext(p => p.Age <= 50);
        await Assert.That(_results.Data.Count).IsEqualTo(50).Because("Should be 50 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 update messages");

        await Assert.That(_results.Data.Items.All(p => p.Age <= 50)).IsTrue();
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        _source.AddRange(people);
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
        _filter.Dispose();
    }

    [Test]
    public async Task ReevaluateFilter()
    {
        //re-evaluate for inline changes
        var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray();

        _source.AddRange(people);
        await Assert.That(_results.Data.Count).IsEqualTo(80).Because("Should be 80 people in the cache");

        foreach (var person in people)
        {
            person.Age += 10;
        }

        _filter.OnNext(p => p.Age > 20);

        await Assert.That(_results.Data.Count).IsEqualTo(90);
        await Assert.That(_results.Messages.Count).IsEqualTo(2);
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(80);
        await Assert.That(_results.Messages[1].Adds).IsEqualTo(90);

        foreach (var person in people)
        {
            person.Age -= 10;
        }

        _filter.OnNext(p => p.Age > 20);

        await Assert.That(_results.Data.Count).IsEqualTo(80).Because("Should be 80 people in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(3).Because("Should be 3 update messages");
    }

    [Test]
    public async Task Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        _source.Add(person);
        _source.Remove(person);

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
                updater.Add(new Person(key, 50));
                updater.Add(new Person(key, 52));
                updater.Add(new Person(key, 53));
                //    updater.Remove(key);
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(3).Because("Should be 3 adds");
    }

    [Test]
    public async Task UpdateMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.Add(newperson);
        _source.Replace(newperson, updated);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(_results.Messages[1].Replaced).IsEqualTo(1).Because("Should be 1 update");
    }

    [Test]
    public async Task UpdateNotMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 10);
        var updated = new Person(key, 11);

        _source.Add(newperson);
        _source.Replace(newperson, updated);

        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("Should be no updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should nothing cached");
    }

    [Test]
    public async Task VeryLargeDataSet()
    {
        var filter = new ReactiveUI.Primitives.Signals.StateSignal<Func<int, bool>>(i => false);
        var source = new SourceList<int>();

        var result = source.Connect().Filter(filter, ListFilterPolicy.ClearAndReplace).AsObservableList();
        source.AddRange(Enumerable.Range(1, 250000));

        filter.OnNext(i => true);
        filter.OnNext(i => false);
    }
}
