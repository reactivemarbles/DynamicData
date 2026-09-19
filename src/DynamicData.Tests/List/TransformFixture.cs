using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class TransformFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithGender> _results;

    private readonly ISourceList<Person> _source;

    private readonly Func<Person, PersonWithGender> _transformFactory = p =>
    {
        var gender = p.Age % 2 == 0 ? "M" : "F";
        return new PersonWithGender(p, gender);
    };

    public TransformFixture()
    {
        _source = new SourceList<Person>();
        _results = new ChangeSetAggregator<PersonWithGender>(_source.Connect().Transform(_transformFactory));
    }

    [Test]
    public async Task Add()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(_transformFactory(person)).Because("Should be same person");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        _source.AddRange(people);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should return 100 adds");

        var transformed = people.Select(_transformFactory).OrderBy(p => p.Age).ToArray();
        await Assert.That(_results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(transformed).Because("Incorrect transform result");
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddRange(people);
        _source.Clear();

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should be 80 addes");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(100).Because("Should be 80 removes");
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

        _source.Add(person);
        _source.Remove(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 80 addes");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(1).Because("Should be 80 removes");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task RemoveWithoutIndex()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        var results = _source.Connect().RemoveIndex().Transform(_transformFactory).AsAggregator();

        _source.Add(person);
        _source.Remove(person);

        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 addes");
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1).Because("Should be 1 removes");
        await Assert.That(results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task SameKeyChanges()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

        _source.AddRange(people);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(10).Because("Should return 10 adds");
        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should result in 10 records");
    }

    [Test]
    public async Task Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.Add(newperson);
        _source.Add(updated);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(_results.Messages[0].Replaced).IsEqualTo(0).Because("Should be 1 update");
    }

    [Test]
    public async Task MultipleSubscribersShouldNotShareState()
    {
        _source.AddRange(new Person[]
        {
            new ("Adult1", 50),
            new ("Adult2", 51)
        });

        var transformed = _source.Connect()
            .Transform(o => o);

        // will throw if state of ChangeAwareList is shared
        transformed.Transform(o => o).Subscribe();
        transformed.Transform(o => o).Subscribe();
    }

    [Test]
    public async Task TransformOnRefresh()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age)
            .Transform(v => v, transformOnRefresh: true).AsAggregator();
        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        items[0].Age = 10;
        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(2);

        await Assert.That(results.Messages[1].First().Reason).IsEqualTo(ListChangeReason.Replace);

        //remove an item and check no change is fired
        var toRemove = items[1];
        list.Remove(toRemove);
        await Assert.That(results.Data.Count).IsEqualTo(99);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        toRemove.Age = 100;
        await Assert.That(results.Messages.Count).IsEqualTo(3);

        //add it back in and check it updates
        list.Add(toRemove);
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        toRemove.Age = 101;
        await Assert.That(results.Messages.Count).IsEqualTo(5);

        await Assert.That(results.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Replace);
    }

    [Test]
    public async Task TransformOnReplace()
    {
        // Arrange
        var person1 = new Person("Bob", 37);
        var person2 = new Person("Bob", 38);

        _source.Add(person1);

        // Act
        _source.Replace(person1, person2);

        // Assert
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 messages");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(_transformFactory(person2)).Because("Should be same person");
    }

    [Test]
    public async Task TransformOnReplaceWithoutIndex()
    {
        // Arrange
        var person1 = new Person("Bob", 37);
        var person2 = new Person("Bob", 38);
        var results = _source.Connect().RemoveIndex().Transform(_transformFactory).AsAggregator();
        _source.Add(person1);

        // Act
        _source.Replace(person1, person2);

        // Assert
        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("Should be 2 messages");
        await Assert.That(results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(results.Data.Items[0]).IsEqualTo(_transformFactory(person2)).Because("Should be same person");
    }
}
