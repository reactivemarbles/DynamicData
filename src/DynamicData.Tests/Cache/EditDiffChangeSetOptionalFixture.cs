using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Tests.Cache;

public class EditDiffChangeSetOptionalFixture
{
    private static readonly ReactiveUI.Primitives.Optional<Person> s_noPerson = ReactiveUI.Primitives.Optional<Person>.None;

    private const int MaxItems = 1097;

    [Test]
    [Description("Required to maintain test coverage percentage")]
    public async Task NullChecksArePerformed()
    {
        Action actionNullKeySelector = () => Observable.Empty<ReactiveUI.Primitives.Optional<Person>>().EditDiff<Person, int>(null!);
        Action actionNullObservable = () => default(IObservable<ReactiveUI.Primitives.Optional<Person>>)!.EditDiff<Person, int>(null!);

        await Assert.That(actionNullKeySelector).Throws<ArgumentNullException>().WithParameterName("keySelector");
        await Assert.That(actionNullObservable).Throws<ArgumentNullException>().WithParameterName("source");
    }

    [Test]
    public async Task OptionalSomeCreatesAddChange()
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = Observable.Return(optional);

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OptionalNoneCreatesRemoveChange()
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = new[] { optional, s_noPerson }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(0);
    }

    [Test]
    public async Task OptionalSomeWithSameKeyCreatesUpdateChange()
    {
        // having
        var optional1 = CreatePerson(0, "Name");
        var optional2 = CreatePerson(0, "Update");
        var optObservable = new[] { optional1, optional2 }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(0);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(1);
    }

    [Test]
    public async Task OptionalSomeWithSameReferenceCreatesNoChanges()
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = new[] { optional, optional }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task OptionalSomeWithSameCreatesNoChanges()
    {
        // having
        var optional1 = CreatePerson(0, "Name");
        var optional2 = CreatePerson(0, "Name");
        var optObservable = new[] { optional1, optional2 }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id, new PersonComparer());
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task OptionalSomeWithDifferentKeyCreatesAddRemoveChanges()
    {
        // having
        var optional1 = CreatePerson(0, "Name");
        var optional2 = CreatePerson(1, "Update");
        var optObservable = new[] { optional1, optional2 }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(1);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(1);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(1);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(0);
    }
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ResultCompletesIfAndOnlyIfSourceCompletes(bool completeSource)
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = Observable.Return(optional);
        if (!completeSource)
        {
            optObservable = optObservable.Concat(Observable.Never<ReactiveUI.Primitives.Optional<Person>>());
        }
        bool completed = false;

        // when
        using var results = optObservable.Subscribe(_ => { }, () => completed = true);

        // then
        await Assert.That(completed).IsEqualTo(completeSource);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ResultFailsIfAndOnlyIfSourceFails(bool failSource)
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = Observable.Return(optional);
        var testException = new Exception("Test");
        if (failSource)
        {
            optObservable = optObservable.Concat(Observable.Throw<ReactiveUI.Primitives.Optional<Person>>(testException));
        }
        var receivedError = default(Exception);

        // when
        using var results = optObservable.Subscribe(_ => { }, err => receivedError = err);

        // then
        await Assert.That(receivedError).IsEqualTo(failSource ? testException : default);
    }

    private static ReactiveUI.Primitives.Optional<Person> CreatePerson(int id, string name) => ReactiveUI.Primitives.Optional<Person>.Some(new Person(id, name));

    private class PersonComparer : IEqualityComparer<Person>
    {
        public bool Equals([DisallowNull] Person x, [DisallowNull] Person y) =>
            EqualityComparer<string>.Default.Equals(x.Name, y.Name) && EqualityComparer<int>.Default.Equals(x.Id, y.Id);
        [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Suppressed for Net 9.0")]
        public int GetHashCode([DisallowNull] Person obj) => throw new NotImplementedException();
    }

    private class Person(int id, string name)
    {
        public int Id { get; } = id;

        public string Name { get; } = name;
    }
}
