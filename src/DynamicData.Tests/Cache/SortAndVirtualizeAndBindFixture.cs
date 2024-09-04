using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;


public sealed class SortAndVirtualizeAndBindWithImplicitOptionsFixtureReadOnlyCollection : SortAndVirtualizeAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {

        var aggregator = Source.Connect()
            .SortAndVirtualize(Comparer, VirtualRequests)
            // no sort and bind options. These are extracted from the SortAndVirtualize context
            .Bind(out var list)
            .AsAggregator();

        return (aggregator, list);
    }
}

public sealed class SortAndVirtualizeAndBindFixtureReadOnlyCollection : SortAndVirtualizeAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {

        var aggregator = Source.Connect()
            .SortAndVirtualize(Comparer, VirtualRequests)
            .Bind(out var list, new SortAndBindOptions())
            .AsAggregator();

        return (aggregator, list);
    }
}

public sealed class SortAndVirtualizeAndBindWithImplicitOptionsFixture : SortAndVirtualizeAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {
        var list = new List<Person>();

        var aggregator = Source.Connect()
            .SortAndVirtualize(Comparer, VirtualRequests)
            // no sort and bind options. These are extracted from the SortAndVirtualize context
            .Bind(list)
            .AsAggregator();

        return (aggregator, list);
    }
}

public sealed class SortAndVirtualizeAndBindFixture : SortAndVirtualizeAndBindFixtureBase
{
    protected override (ChangeSetAggregator<Person, string> aggregator, IList<Person> list) SetUpTests()
    {
        var list = new List<Person>();

        var aggregator = Source.Connect()
            .SortAndVirtualize(Comparer, VirtualRequests)
            .SortAndBind(list, new SortAndBindOptions())
            .AsAggregator();

        return (aggregator, list);
    }
}

public abstract class SortAndVirtualizeAndBindFixtureBase : IDisposable
{

    protected readonly SourceCache<Person, string> Source = new(p => p.Name);
    protected readonly IComparer<Person> Comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);
    protected readonly ISubject<IVirtualRequest> VirtualRequests = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 25));

    protected readonly ChangeSetAggregator<Person, string> Aggregator;
    protected readonly IList<Person> List;

    protected SortAndVirtualizeAndBindFixtureBase()
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


    [Fact]
    public void InitialBatches()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Take(25).ToList();
        List.Should().BeEquivalentTo(expectedResult);


        VirtualRequests.OnNext(new VirtualRequest(25, 50));
        expectedResult = people.OrderBy(p => p, Comparer).Skip(25).Take(50).ToList();
        List.Should().BeEquivalentTo(expectedResult);


        VirtualRequests.OnNext(new VirtualRequest(40, 50));
        expectedResult = people.OrderBy(p => p, Comparer).Skip(40).Take(50).ToList();
        List.Should().BeEquivalentTo(expectedResult);
    }






    [Fact]
    public void OverlappingShift()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        Source.AddOrUpdate(people);

        VirtualRequests.OnNext(new VirtualRequest(10, 30));

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, Comparer).Skip(10).Take(30).ToList();
        List.Should().BeEquivalentTo(expectedResult);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
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
        List.SequenceEqual(expectedResult).Should().Be(true);
    }

    public void Dispose()
    {
        Source.Dispose();
        Aggregator.Dispose();
        VirtualRequests.OnCompleted();
    }
}
