#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

[InheritsTests]
public sealed class SortAndPageAndBindWithImplicitOptionsFixtureReadOnlyCollection : SortAndPageAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {

        var aggregator = Source.Connect()
            .SortAndPage(Comparer, PageRequests)
            // no sort and bind options. These are extracted from the SortAndPage context
            .Bind(out var list)
            .AsAggregator();

        return (aggregator, list);
    }
}

[InheritsTests]
public sealed class SortAndPageAndBindFixtureReadOnlyCollection : SortAndPageAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {

        var aggregator = Source.Connect()
            .SortAndPage(Comparer, PageRequests)
            .Bind(out var list, new SortAndBindOptions())
            .AsAggregator();

        return (aggregator, list);
    }
}

[InheritsTests]
public sealed class SortAndPageAndBindWithImplicitOptionsFixture : SortAndPageAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {
        var list = new List<Person>();

        var aggregator = Source.Connect()
            .SortAndPage(Comparer, PageRequests)
            // no sort and bind options. These are extracted from the SortAndPage context
            .Bind(list)
            .AsAggregator();

        return (aggregator, list);
    }
}

[InheritsTests]
public sealed class SortAndPageAndBindFixture : SortAndPageAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {
        var list = new List<Person>();

        var aggregator = Source.Connect()
            .SortAndPage(Comparer, PageRequests)
            .SortAndBind(list, new SortAndBindOptions())
            .AsAggregator();

        return (aggregator, list);
    }
}

public abstract class SortAndPageAndBindFixtureBase : IDisposable
{

    protected readonly SourceCache<Person, string> Source = new(p => p.Name);
    protected readonly IComparer<Person> Comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);
    private protected readonly ReactiveUI.Primitives.Signals.ISignal<IPageRequest> PageRequests = new ReactiveUI.Primitives.Signals.StateSignal<IPageRequest>(new PageRequest(0, 25));

    protected readonly ChangeSetAggregator<Person, string> Aggregator;
    protected readonly IList<Person> List;

    protected SortAndPageAndBindFixtureBase()
    {
        // It's ok in this case to call VirtualMemberCallInConstructor

#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        var args = SetUpTests();
#pragma warning restore CA2214

        Aggregator = args.aggregator;
        List = args.list;
    }

    protected abstract (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests();

    private static async Task AssertPeopleAreEquivalent(IEnumerable<Person> actual, IEnumerable<Person> expected) =>
        await Assert.That(actual).IsEquivalentTo(expected, Person.NameAgeGenderComparer, TUnit.Assertions.Enums.CollectionOrdering.Matching);

    [Test]
    public async Task PageGreaterThanNumberOfPagesAvailable()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // should select the last page
        PageRequests.OnNext(new PageRequest(10, 25));

        var expectedResult = people.OrderBy(p => p, Comparer).Skip(75).Take(25).ToList();

        await AssertPeopleAreEquivalent(List, expectedResult);
    }

    [Test]
    public async Task OverlappingShift()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        PageRequests.OnNext(new PageRequest(3, 10));

        // for first batch, it should use the results of the _PageRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Skip(20).Take(10).ToList();
        await AssertPeopleAreEquivalent(List, expectedResult);
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
        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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
        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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
        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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
        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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

        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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

        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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

        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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

        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
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

        await Assert.That(List.SequenceEqual(expectedResult, Person.NameAgeGenderComparer)).IsTrue();
    }

    public void Dispose()
    {
        Source.Dispose();
        Aggregator.Dispose();
        PageRequests.OnCompleted();
        PageRequests.Dispose();
    }
}
