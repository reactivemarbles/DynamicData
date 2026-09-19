namespace DynamicData.Tests.Cache;

public class EditDiffChangeSetFixture
{
    private const int MaxItems = 1097;

    [Test]
    public async Task NullChecksArePerformed()
    {
        await Assert.That(() => Observable.Empty<IEnumerable<Person>>().EditDiff<Person, int>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => default(IObservable<IEnumerable<Person>>)!.EditDiff<Person, int>(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ItemsFromEnumerableAreAddedToChangeSet()
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = Observable.Return(enumerable);

        // when
        var observableChangeSet = enumObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(MaxItems);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ItemsRemovedFromEnumerableAreRemovedFromChangeSet()
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = new[] { enumerable, Enumerable.Empty<Person>() }.ToObservable();

        // when
        var observableChangeSet = enumObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(MaxItems);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(MaxItems);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(0);
    }

    [Test]
    public async Task ItemsUpdatedAreUpdatedInChangeSet()
    {
        // having
        var enumerable1 = CreatePeople(0, MaxItems * 2, "Name");
        var enumerable2 = CreatePeople(MaxItems, MaxItems, "Update");
        var enumObservable = new[] { enumerable1, enumerable2 }.ToObservable();

        // when
        var observableChangeSet = enumObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(MaxItems);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(MaxItems * 2);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(MaxItems);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(MaxItems);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ResultCompletesIfAndOnlyIfSourceCompletes(bool completeSource)
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = Observable.Return(enumerable);
        if (!completeSource)
        {
            enumObservable = enumObservable.Concat(Observable.Never<IEnumerable<Person>>());
        }
        bool completed = false;

        // when
        using var results = enumObservable.EditDiff(p => p.Id).Subscribe(_ => { }, () => completed = true);

        // then
        await Assert.That(completed).IsEqualTo(completeSource);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ResultFailsIfAndOnlyIfSourceFails(bool failSource)
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = Observable.Return(enumerable);
        var testException = new Exception("Test");
        if (failSource)
        {
            enumObservable = enumObservable.Concat(Observable.Throw<IEnumerable<Person>>(testException));
        }
        var receivedError = default(Exception);

        // when
        using var results = enumObservable.EditDiff(p => p.Id).Subscribe(_ => { }, err => receivedError = err);

        // then
        await Assert.That(receivedError).IsEqualTo(failSource ? testException : default);
    }

    private static Person CreatePerson(int id, string name) => new(id, name);

    private static IEnumerable<Person> CreatePeople(int baseId, int count, string baseName) =>
        Enumerable.Range(baseId, count).Select(i => CreatePerson(i, baseName + i));

    private class Person(int id, string name)
    {
        public int Id { get; } = id;

        public string Name { get; } = name;
    }
}
