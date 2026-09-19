#if REACTIVE_TESTS
using DynamicData.Reactive.PLinq;
#else
using DynamicData.PLinq;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformFixtureParallel : IDisposable
{
    private readonly Func<Person, PersonWithGender> _transformFactory = p =>
    {
        var gender = p.Age % 2 == 0 ? "M" : "F";
        return new PersonWithGender(p, gender);
    };

    private readonly ChangeSetAggregator<PersonWithGender, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public TransformFixtureParallel()
    {
        _source = new SourceCache<Person, string>(p => p.Name);

        var pTransform = _source.Connect().Transform(_transformFactory, new ParallelisationOptions(ParallelType.Parallelise));
        _results = new ChangeSetAggregator<PersonWithGender, string>(pTransform);
    }

    [Test]
    public async Task Add()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(_transformFactory(person)).Because("Should be same person");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should return 100 adds");

        var transformed = people.Select(_transformFactory).ToArray();

        await Assert.That(_results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(transformed).Because("Incorrect transform result");
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddOrUpdate(people);

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
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();
        _source.AddOrUpdate(people);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should return 1 adds");
        await Assert.That(_results.Messages[0].Updates).IsEqualTo(9).Because("Should return 9 adds");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should result in 1 record");

        var lastTransformed = _transformFactory(people.Last());
        var onlyItemInCache = _results.Data.Items[0];

        // TODO: This is not producing consitent results, the lastTransformed item should be equal to the onlyItemInCache
        await Assert.That(onlyItemInCache).IsEqualTo(lastTransformed).Because("Incorrect transform result");
    }

    [Test]
    public async Task Update()
    {
        const string key = "Adult1";
        var newPerson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.AddOrUpdate(newPerson);
        _source.AddOrUpdate(updated);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(_results.Messages[1].Updates).IsEqualTo(1).Because("Should be 1 update");
    }
}
