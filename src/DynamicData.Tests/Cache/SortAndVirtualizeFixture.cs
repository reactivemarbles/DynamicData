#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

[InheritsTests]
public sealed class SortAndVirtualizeWithComparerChangesFixture : SortAndVirtualizeFixtureBase
{
    private ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>> _comparerSubject;

    private readonly IComparer<Person> _descComparer = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

    protected override ChangeSetAggregator<Person, string, VirtualContext<Person>> SetUpTests()
    {
        _comparerSubject = new ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>>(Comparer);

        return Source.Connect()
            .SortAndVirtualize(_comparerSubject, VirtualRequests)
            .AsAggregator();
    }

    [Test]
    public async Task ChangeComparer()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await AssertPeopleAreEquivalent(actualResult, expectedResult);

        // change the comparer
        _comparerSubject.OnNext(_descComparer);

        expectedResult = people.OrderBy(p => p, _descComparer).Take(25).ToList();
        actualResult = Aggregator.Data.Items.OrderBy(p => p, _descComparer);
        await AssertPeopleAreEquivalent(actualResult, expectedResult);
    }

    [Test]
    public async Task ChangeComparerDataSetSmallerThanVirtualSize()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person($"P{i:00}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Take(10).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await AssertPeopleAreEquivalent(actualResult, expectedResult);

        await Assert.That(Aggregator.Messages[0].All(c => c.Reason == ChangeReason.Add)).IsTrue();

        // change the comparer
        _comparerSubject.OnNext(_descComparer);

        expectedResult = people.OrderBy(p => p, _descComparer).Take(10).ToList();
        actualResult = Aggregator.Data.Items.OrderBy(p => p, _descComparer);
        await AssertPeopleAreEquivalent(actualResult, expectedResult);

        await Assert.That(Aggregator.Messages[1].All(c => c.Reason == ChangeReason.Refresh)).IsTrue();
    }
}

[InheritsTests]
public sealed class SortAndVirtualizeFixture : SortAndVirtualizeFixtureBase
{
    protected override ChangeSetAggregator<Person, string, VirtualContext<Person>> SetUpTests() =>
        Source.Connect()
            .SortAndVirtualize(Comparer, VirtualRequests)
            .AsAggregator();
}

public abstract class SortAndVirtualizeFixtureBase : IDisposable
{

    protected readonly SourceCache<Person, string> Source = new(p => p.Name);
    protected readonly IComparer<Person> Comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);
    private protected readonly ReactiveUI.Primitives.Signals.ISignal<IVirtualRequest> VirtualRequests = new ReactiveUI.Primitives.Signals.StateSignal<IVirtualRequest>(new VirtualRequest(0, 25));

    protected readonly ChangeSetAggregator<Person, string, VirtualContext<Person>> Aggregator;

    protected SortAndVirtualizeFixtureBase()
    {
        // It's ok in this case to call VirtualMemberCallInConstructor

#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        Aggregator = SetUpTests();
#pragma warning restore CA2214
    }

    protected abstract ChangeSetAggregator<Person, string, VirtualContext<Person>> SetUpTests();

    protected static async Task AssertPeopleAreEquivalent(IEnumerable<Person> actual, IEnumerable<Person> expected) =>
        await Assert.That(actual).IsEquivalentTo(expected, Person.NameAgeGenderComparer, TUnit.Assertions.Enums.CollectionOrdering.Matching);

    [Test]
    public async Task InitialBatches()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await AssertPeopleAreEquivalent(actualResult, expectedResult);

        VirtualRequests.OnNext(new VirtualRequest(25, 50));

        expectedResult = people.OrderBy(p => p, Comparer).Skip(25).Take(50).ToList();
        actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await AssertPeopleAreEquivalent(actualResult, expectedResult);
    }

    [Test]
    public async Task OverlappingShift()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        VirtualRequests.OnNext(new VirtualRequest(10, 30));

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Skip(10).Take(30).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await AssertPeopleAreEquivalent(actualResult, expectedResult);
    }

    [Test]
    public async Task AddFirstInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("_FirstPerson", 1);
        Source.AddOrUpdate(person);

        await Assert.That(Aggregator.Messages.Count).IsEqualTo(2);

        var changes = Aggregator.Messages[1];
        await Assert.That(changes.Count).IsEqualTo(2);

        var firstChange = changes.First();
        await Assert.That(firstChange.Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(Person.NameAgeGenderComparer.Equals(firstChange.Current, new Person("P025", 25))).IsTrue();

        var secondChange = changes.Skip(1).First();
        await Assert.That(secondChange.Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(secondChange.Current).IsEqualTo(person);

        // check for correctness of resulting collection
        people.Add(person);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    [Test]
    public async Task AddOutsideOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at end
        var person = new Person("X_Last", 100);
        Source.AddOrUpdate(person);

        // only the initials message should have been received
        await Assert.That(Aggregator.Messages.Count).IsEqualTo(1);

        people.Add(person);
        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    [Test]
    public async Task UpdateMoveOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);
        var oldPerson = people.Single(p => p.Name == "P012");

        // Change an item so it moves from in range to out of range
        var person = new Person("P012", 50);
        Source.AddOrUpdate(person);

        await Assert.That(Aggregator.Messages.Count).IsEqualTo(2);

        var changes = Aggregator.Messages[1];
        await Assert.That(changes.Count).IsEqualTo(2);

        var firstChange = changes.First();
        await Assert.That(firstChange.Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(ReferenceEquals(firstChange.Current, oldPerson)).IsTrue();

        var secondChange = changes.Skip(1).First();
        await Assert.That(secondChange.Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(Person.NameAgeGenderComparer.Equals(secondChange.Current, new Person("P026", 26))).IsTrue();

        // check for correctness of resulting collection
        people = people.OrderBy(p => p, Comparer).ToList();
        people[11] = person;

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }
    [Test]
    public async Task UpdateStayRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // Update an item, but keep it withing the expected virtual range.
        var person = new Person("P012", -1);
        Source.AddOrUpdate(person);

        await Assert.That(Aggregator.Messages.Count).IsEqualTo(2);

        var changes = Aggregator.Messages[1];
        await Assert.That(changes.Count).IsEqualTo(1);

        var firstChange = changes.First();
        await Assert.That(firstChange.Reason).IsEqualTo(ChangeReason.Update);
        await Assert.That(Person.NameAgeGenderComparer.Equals(firstChange.Current, new Person("P012", -1))).IsTrue();
        await Assert.That(Person.NameAgeGenderComparer.Equals(firstChange.Previous.Value, new Person("P012", 12))).IsTrue();

        // check for correctness of resulting collection
        people = people.OrderBy(p => p, Comparer).ToList();
        people[11] = person;

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    [Test]
    public async Task UpdateOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("P050", 100);
        Source.AddOrUpdate(person);

        // only the initials message should have been received
        await Assert.That(Aggregator.Messages.Count).IsEqualTo(1);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    [Test]
    public async Task RemoveRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // remove an element from the active range
        var person = new Person("P012", 12);
        Source.Remove(person);

        await Assert.That(Aggregator.Messages.Count).IsEqualTo(2);

        var changes = Aggregator.Messages[1];
        await Assert.That(changes.Count).IsEqualTo(2);

        var firstChange = changes.First();
        await Assert.That(firstChange.Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(firstChange.Current).IsEqualTo(person);

        var secondChange = changes.Skip(1).First();
        await Assert.That(secondChange.Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(Person.NameAgeGenderComparer.Equals(secondChange.Current, new Person("P026", 26))).IsTrue();

        // check for correctness of resulting collection
        people.Remove(person);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    [Test]
    public async Task RemoveOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("P050", 50);
        Source.Remove(person);

        // only the initials message should have been received
        await Assert.That(Aggregator.Messages.Count).IsEqualTo(1);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    [Test]
    public async Task RefreshInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");
        Source.Refresh(person);

        await Assert.That(Aggregator.Messages.Count).IsEqualTo(2);

        var changes = Aggregator.Messages[1];
        await Assert.That(changes.Count).IsEqualTo(1);

        var firstChange = changes.First();
        await Assert.That(firstChange.Reason).IsEqualTo(ChangeReason.Refresh);
    }

    [Test]
    public async Task RefreshWithInlineChangeInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");

        // The item will move within the virtual range, so be propagated as a refresh
        person.Age = 5;
        Source.Refresh(person);

        await Assert.That(Aggregator.Messages.Count).IsEqualTo(2);

        var changes = Aggregator.Messages[1];
        await Assert.That(changes.Count).IsEqualTo(1);

        var firstChange = changes.First();
        await Assert.That(firstChange.Reason).IsEqualTo(ChangeReason.Refresh);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    [Test]
    public async Task RefreshWithInlineChangeOutsideRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");

        // The item will move outside the virtual range, resulting in a remove and index shift
        person.Age = 50;
        Source.Refresh(person);

        await Assert.That(Aggregator.Messages.Count).IsEqualTo(2);

        var changes = Aggregator.Messages[1];
        await Assert.That(changes.Count).IsEqualTo(2);

        var firstChange = changes.First();
        await Assert.That(firstChange.Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(Person.NameAgeGenderComparer.Equals(firstChange.Current, new Person("P012", 50))).IsTrue();

        var secondChange = changes.Skip(1).First();
        await Assert.That(secondChange.Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(Person.NameAgeGenderComparer.Equals(secondChange.Current, new Person("P026", 26))).IsTrue();

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        await Assert.That(actualResult.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    public void Dispose()
    {
        Source.Dispose();
        Aggregator.Dispose();
        VirtualRequests.OnCompleted();
        VirtualRequests.Dispose();
    }
}
