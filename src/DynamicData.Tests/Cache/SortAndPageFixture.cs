using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public sealed class SortAndPageWithComparerChangesFixture : SortAndPageFixtureBase
{
    private BehaviorSubject<IComparer<Person>> _comparerSubject;

    private readonly IComparer<Person> _descComparer = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

    protected override ChangeSetAggregator<Person, string, PageContext<Person>> SetUpTests()
    {
        _comparerSubject = new BehaviorSubject<IComparer<Person>>(Comparer);

        return Source.Connect()
            .SortAndPage(_comparerSubject, PageRequests)
            .AsAggregator();
    }

    [Fact]
    public void ChangeComparer()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // for first batch, it should use the results of the _PageRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);

        // change the comparer 
        _comparerSubject.OnNext(_descComparer);

        expectedResult = people.OrderBy(p => p, _descComparer).Take(25).ToList();
        actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}

public sealed class SortAndPageFixture : SortAndPageFixtureBase
{
    protected override ChangeSetAggregator<Person, string, PageContext<Person>> SetUpTests() =>
        Source.Connect()
            .SortAndPage(Comparer, PageRequests)
            .AsAggregator();
}

public abstract class SortAndPageFixtureBase : IDisposable
{

    protected readonly SourceCache<Person, string> Source = new(p => p.Name);
    protected readonly IComparer<Person> Comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);
    protected readonly ISubject<IPageRequest> PageRequests = new BehaviorSubject<IPageRequest>(new PageRequest(1, 25));

    protected readonly ChangeSetAggregator<Person, string, PageContext<Person>> Aggregator;


    protected SortAndPageFixtureBase()
    {
        // It's ok in this case to call VirtualMemberCallInConstructor

#pragma warning disable CA2214
        // ReSharper disable once VirtualMemberCallInConstructor
        Aggregator = SetUpTests();
#pragma warning restore CA2214
    }


    protected abstract ChangeSetAggregator<Person, string, PageContext<Person>> SetUpTests();


    [Fact]
    public void InitialBatches()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // for first batch, it should use the results of the _PageRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);


        PageRequests.OnNext(new PageRequest(3, 25));

        expectedResult = people.OrderBy(p => p, Comparer).Skip(50).Take(25).ToList();
        actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void ThrowsForNegativePage() => Assert.Throws<ArgumentException>(() => PageRequests.OnNext(new PageRequest(-1, 1)));

    [Fact]
    public void ThrowsForNegativeSizeParameters() => Assert.Throws<ArgumentException>(() => PageRequests.OnNext(new PageRequest(1, -1)));



    [Fact]
    public void PageGreaterThanNumberOfPagesAvailable()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // should select the last page
        PageRequests.OnNext(new PageRequest(10, 25));

        var expectedResult = people.OrderBy(p => p, Comparer).Skip(75).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }


    [Fact]
    public void OverlappingShift()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        PageRequests.OnNext(new PageRequest(3, 10));

        // for first batch, it should use the results of the _PageRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Skip(20).Take(10).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void AddFirstInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("_FirstPerson", 1);
        Source.AddOrUpdate(person);

        Aggregator.Messages.Count.Should().Be(2);

        var changes = Aggregator.Messages[1];
        changes.Count.Should().Be(2);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Remove);
        firstChange.Current.Should().Be(new Person("P025", 25));

        var secondChange = changes.Skip(1).First();
        secondChange.Reason.Should().Be(ChangeReason.Add);
        secondChange.Current.Should().Be(person);

        // check for correctness of resulting collection
        people.Add(person);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }


    [Fact]
    public void AddOutsideOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at end
        var person = new Person("X_Last", 100);
        Source.AddOrUpdate(person);

        // only the initials message should have been received
        Aggregator.Messages.Count.Should().Be(1);


        people.Add(person);
        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    [Fact]
    public void UpdateMoveOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // Change an item so it moves from in range to out of range
        var person = new Person("P012", 50);
        Source.AddOrUpdate(person);

        Aggregator.Messages.Count.Should().Be(2);

        var changes = Aggregator.Messages[1];
        changes.Count.Should().Be(2);


        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Remove);
        firstChange.Current.Should().Be(new Person("P012", 50));

        var secondChange = changes.Skip(1).First();
        secondChange.Reason.Should().Be(ChangeReason.Add);
        secondChange.Current.Should().Be(new Person("P026", 26));

        // check for correctness of resulting collection
        people = people.OrderBy(p => p, Comparer).ToList();
        people[11] = person;

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }
    [Fact]
    public void UpdateStayRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // Update an item, but keep it withing the expected virtual range.
        var person = new Person("P012", -1);
        Source.AddOrUpdate(person);

        Aggregator.Messages.Count.Should().Be(2);

        var changes = Aggregator.Messages[1];
        changes.Count.Should().Be(1);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Update);
        firstChange.Current.Should().Be(new Person("P012", -1));
        firstChange.Previous.Value.Should().Be(new Person("P012", 12));

        // check for correctness of resulting collection
        people = people.OrderBy(p => p, Comparer).ToList();
        people[11] = person;

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }



    [Fact]
    public void UpdateOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("P050", 100);
        Source.AddOrUpdate(person);

        // only the initials message should have been received
        Aggregator.Messages.Count.Should().Be(1);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }


    [Fact]
    public void RemoveRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // remove an element from the active range
        var person = new Person("P012", 12);
        Source.Remove(person);

        Aggregator.Messages.Count.Should().Be(2);

        var changes = Aggregator.Messages[1];
        changes.Count.Should().Be(2);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Remove);
        firstChange.Current.Should().Be(person);

        var secondChange = changes.Skip(1).First();
        secondChange.Reason.Should().Be(ChangeReason.Add);
        secondChange.Current.Should().Be(new Person("P026", 26));

        // check for correctness of resulting collection
        people.Remove(person);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    [Fact]
    public void RemoveOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("P050", 50);
        Source.Remove(person);

        // only the initials message should have been received
        Aggregator.Messages.Count.Should().Be(1);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }


    [Fact]
    public void RefreshInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");
        Source.Refresh(person);

        Aggregator.Messages.Count.Should().Be(2);

        var changes = Aggregator.Messages[1];
        changes.Count.Should().Be(1);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Refresh);
    }

    [Fact]
    public void RefreshWithInlineChangeInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");

        // The item will move within the virtual range, so be propagated as a refresh
        person.Age = 5;
        Source.Refresh(person);

        Aggregator.Messages.Count.Should().Be(2);

        var changes = Aggregator.Messages[1];
        changes.Count.Should().Be(1);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Refresh);

        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    [Fact]
    public void RefreshWithInlineChangeOutsideRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        Source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");

        // The item will move outside the virtual range, resulting in a remove and index shift
        person.Age = 50;
        Source.Refresh(person);

        Aggregator.Messages.Count.Should().Be(2);

        var changes = Aggregator.Messages[1];
        changes.Count.Should().Be(2);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Remove);
        firstChange.Current.Should().Be(new Person("P012", 50));

        var secondChange = changes.Skip(1).First();
        secondChange.Reason.Should().Be(ChangeReason.Add);
        secondChange.Current.Should().Be(new Person("P026", 26));


        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        var actualResult = Aggregator.Data.Items.OrderBy(p => p, Comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    public void Dispose()
    {
        Source.Dispose();
        Aggregator.Dispose();
        PageRequests.OnCompleted();
    }
}
