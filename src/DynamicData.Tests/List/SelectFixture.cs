#if REACTIVE_TESTS
using DynamicData.Reactive.Alias;
#else
using DynamicData.Alias;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class SelectFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithGender> _results;

    private readonly ISourceList<Person> _source;

    private readonly Func<Person, PersonWithGender> _transformFactory = p =>
    {
        var gender = p.Age % 2 == 0 ? "M" : "F";
        return new PersonWithGender(p, gender);
    };

    public SelectFixture()
    {
        _source = new SourceList<Person>();
        _results = new ChangeSetAggregator<PersonWithGender>(_source.Connect().Select(_transformFactory));
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
}
