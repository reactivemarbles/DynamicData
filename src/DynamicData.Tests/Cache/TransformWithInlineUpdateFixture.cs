using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformWithInlineUpdateFixture
{
    [Test]
    public async Task InlineUpdate()
    {
        using var stub = new TransformWithInlineUpdateFixtureStub();
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        var transformedPerson = stub.Results.Data.Items[0];

        var personUpdate = new Person("Adult1", 51);
        stub.Source.AddOrUpdate(personUpdate);

        var updatedTransform = stub.Results.Data.Items[0];

        await Assert.That(updatedTransform.Age).IsEqualTo(personUpdate.Age).Because("Age should be updated from 50 to 51.");
        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(stub.Results.Data.Items[0]).IsSameReferenceAs(transformedPerson).Because("Should be same transformed person instance.");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformWithInlineUpdateFixtureStub();
        stub.Source.AddOrUpdate(people);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(100).Because("Should return 100 adds");

        var transformed = people.Select(stub.TransformFactory).OrderBy(p => p.Age).ToArray();
        await Assert.That(stub.Results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(transformed, Person.NameAgeGenderComparer).Because("Incorrect transform result");
    }

    [Test]
    public async Task Clear()
    {
        using var stub = new TransformWithInlineUpdateFixtureStub();
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        stub.Source.AddOrUpdate(people);
        stub.Source.Clear();

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(100).Because("Should be 80 addes");
        await Assert.That(stub.Results.Messages[1].Removes).IsEqualTo(100).Because("Should be 80 removes");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        using var stub = new TransformWithInlineUpdateFixtureStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should be 80 addes");
        await Assert.That(stub.Results.Messages[1].Removes).IsEqualTo(1).Because("Should be 80 removes");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task TransformOnRefresh()
    {
        using var stub = new TransformWithInlineUpdateFixtureStub(true);
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        var transformedPerson = stub.Results.Data.Items[0];

        person.Age = 51;
        stub.Source.Refresh(person);

        var updatedTransform = stub.Results.Data.Items[0];

        await Assert.That(updatedTransform.Age).IsEqualTo(51).Because("Age should be updated from 50 to 51.");
        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(stub.Results.Data.Items[0]).IsSameReferenceAs(transformedPerson).Because("Should be same transformed person instance.");
    }

    private class TransformWithInlineUpdateFixtureStub : IDisposable
    {
        public TransformWithInlineUpdateFixtureStub(bool transformOnRefresh = false)
        {
            Results = new ChangeSetAggregator<Person, string>(Source.Connect()
                .TransformWithInlineUpdate(TransformFactory, UpdateAction, transformOnRefresh));
        }

        public ChangeSetAggregator<Person, string> Results { get; }

        public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);

        public Action<Person, Person> UpdateAction { get; } = (transformed, current) => transformed.Age = current.Age;

        public Func<Person, Person> TransformFactory { get; } = (p) => new Person(p.Name, p.Age);

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
