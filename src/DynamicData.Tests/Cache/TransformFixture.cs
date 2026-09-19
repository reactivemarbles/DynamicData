using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformFixture
{
    [Test]
    public async Task Add()
    {
        using var stub = new TransformStub();
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(stub.Results.Data.Items[0]).IsEqualTo(stub.TransformFactory(person)).Because("Should be same person");
    }

    [Test]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(people);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(100).Because("Should return 100 adds");

        var transformed = people.Select(stub.TransformFactory).OrderBy(p => p.Age).ToArray();
        await Assert.That(stub.Results.Data.Items.OrderBy(p => p.Age)).IsEquivalentTo(transformed).Because("Incorrect transform result");
    }

    [Test]
    public async Task Clear()
    {
        using var stub = new TransformStub();
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

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should be 80 addes");
        await Assert.That(stub.Results.Messages[1].Removes).IsEqualTo(1).Because("Should be 80 removes");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(0).Because("Should be nothing cached");
    }

    [Test]
    public async Task ReTransformAll()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new ReactiveUI.Primitives.Signals.Signal<Unit>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(Unit.Default);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2);
        await Assert.That(stub.Results.Messages[1].Updates).IsEqualTo(10);

        for (var i = 1; i <= 10; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;

            await Assert.That(updated).IsEqualTo(original);
            await Assert.That(ReferenceEquals(original, updated)).IsFalse();
        }
    }

    [Test]
    public async Task ReTransformSelected()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new ReactiveUI.Primitives.Signals.Signal<Func<Person, bool>>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(person => person.Age <= 5);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2);
        await Assert.That(stub.Results.Messages[1].Updates).IsEqualTo(5);

        for (var i = 1; i <= 5; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;
            await Assert.That(updated).IsEqualTo(original);
            await Assert.That(ReferenceEquals(original, updated)).IsFalse();
        }
    }

    [Test]
    public async Task SameKeyChanges()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

        stub.Source.AddOrUpdate(people);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should return 1 adds");
        await Assert.That(stub.Results.Messages[0].Updates).IsEqualTo(9).Because("Should return 9 adds");
        await Assert.That(stub.Results.Data.Count).IsEqualTo(1).Because("Should result in 1 record");

        var lastTransformed = stub.TransformFactory(people.Last());
        var onlyItemInCache = stub.Results.Data.Items[0];

        await Assert.That(onlyItemInCache).IsEqualTo(lastTransformed).Because("Incorrect transform result");
    }

    [Test]
    public async Task Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(newperson);
        stub.Source.AddOrUpdate(updated);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(stub.Results.Messages[0].Adds).IsEqualTo(1).Because("Should be 1 adds");
        await Assert.That(stub.Results.Messages[1].Updates).IsEqualTo(1).Because("Should be 1 update");
    }

    private class TransformStub : IDisposable
    {
        public TransformStub() => Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().Transform(TransformFactory));

        public TransformStub(IObservable<Unit> retransformer) => Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().Transform(TransformFactory, retransformer));

        public TransformStub(IObservable<Func<Person, bool>> retransformer) => Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().Transform(TransformFactory, retransformer));

        public ChangeSetAggregator<PersonWithGender, string> Results { get; }

        public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);

        public Func<Person, PersonWithGender> TransformFactory { get; } = p => new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
