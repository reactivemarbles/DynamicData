using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;


public class SortAndVirtualizeFixture : IDisposable
{

    private readonly SourceCache<Person, string> _source = new(p => p.Name);
    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

    private readonly ISubject<IVirtualRequest> _virtualRequests= new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 25));
    private readonly ChangeSetAggregator<Person, string, VirtualContext<Person>> _aggregator;

    public SortAndVirtualizeFixture() =>
        _aggregator = _source.Connect()
            .SortAndVirtualize(_comparer, _virtualRequests)
            .AsAggregator();


    [Fact]
    public void InitialBatches()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p=>Guid.NewGuid());
        _source.AddOrUpdate(people);

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);


        _virtualRequests.OnNext(new VirtualRequest(25,50));

        expectedResult = people.OrderBy(p => p, _comparer).Skip(25).Take(50).ToList();
         actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }



    [Fact]
    public void OverlappingShift()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid());
        _source.AddOrUpdate(people);

        _virtualRequests.OnNext(new VirtualRequest(10, 30));

        // for first batch, it should use the results of the _virtualRequests subject (if a behaviour subject is used).
        var expectedResult = people.OrderBy(p => p, _comparer).Skip(10).Take(30).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void AddFirstInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("_FirstPerson", 1);
        _source.AddOrUpdate(person);

        _aggregator.Messages.Count.Should().Be(2);

        var changes = _aggregator.Messages[1];
        changes.Count.Should().Be(2);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Remove);
        firstChange.Current.Should().Be(new Person("P025",25));

        var secondChange = changes.Skip(1).First();
        secondChange.Reason.Should().Be(ChangeReason.Add);
        secondChange.Current.Should().Be(person);

        // check for correctness of resulting collection
        people.Add(person);

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }


    [Fact]
    public void AddOutsideOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        // insert right at end
        var person = new Person("X_Last", 1);
        _source.AddOrUpdate(person);

        // only the initials message should have been received
        _aggregator.Messages.Count.Should().Be(1);

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    [Fact]
    public void UpdateInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("P012", 50);
        _source.AddOrUpdate(person);

        _aggregator.Messages.Count.Should().Be(2);

        var changes = _aggregator.Messages[1];
        changes.Count.Should().Be(1);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Update);
        firstChange.Current.Should().Be(new Person("P012", 50));
        firstChange.Previous.Value.Should().Be(new Person("P012", 12));

        // check for correctness of resulting collection
        people = people.OrderBy(p => p, _comparer).ToList();
        people[11] =person;

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }


    [Fact]
    public void UpdateOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("P050", 100);
        _source.AddOrUpdate(person);

        // only the initials message should have been received
        _aggregator.Messages.Count.Should().Be(1);

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }


    [Fact]
    public void RemoveRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        // remove an element from the active range
        var person = new Person("P012", 12);
        _source.Remove(person);

        _aggregator.Messages.Count.Should().Be(2);

        var changes = _aggregator.Messages[1];
        changes.Count.Should().Be(2);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Remove);
        firstChange.Current.Should().Be(person);

        var secondChange = changes.Skip(1).First();
        secondChange.Reason.Should().Be(ChangeReason.Add);
        secondChange.Current.Should().Be(new Person("P026", 26));

        // check for correctness of resulting collection
        people.Remove(person);

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    [Fact]
    public void RemoveOutOfRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        // insert right at beginning
        var person = new Person("P050", 50);
        _source.Remove(person);

        // only the initials message should have been received
        _aggregator.Messages.Count.Should().Be(1);

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }


    [Fact]
    public void RefreshInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        var person = people.Single(p=>p.Name == "P012");
        _source.Refresh(person);

        _aggregator.Messages.Count.Should().Be(2);

        var changes = _aggregator.Messages[1];
        changes.Count.Should().Be(1);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Refresh);
    }

    [Fact]
    public void RefreshWithInlineChangeInRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");

        // The item will move within the virtual range, so be propagated as a refresh
        person.Age = 5;
        _source.Refresh(person);

        _aggregator.Messages.Count.Should().Be(2);

        var changes = _aggregator.Messages[1];
        changes.Count.Should().Be(1);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Refresh);

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    [Fact]
    public void RefreshWithInlineChangeOutsideRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person($"P{i:000}", i)).OrderBy(p => Guid.NewGuid()).ToList();
        _source.AddOrUpdate(people);

        var person = people.Single(p => p.Name == "P012");

        // The item will move outside the virtual range, resulting in a remove and index shift
        person.Age = 50;
        _source.Refresh(person);

        _aggregator.Messages.Count.Should().Be(2);

        var changes = _aggregator.Messages[1];
        changes.Count.Should().Be(2);

        var firstChange = changes.First();
        firstChange.Reason.Should().Be(ChangeReason.Remove);
        firstChange.Current.Should().Be(new Person("P012", 50));

        var secondChange = changes.Skip(1).First();
        secondChange.Reason.Should().Be(ChangeReason.Add);
        secondChange.Current.Should().Be(new Person("P026", 26));


        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).ToList();
        var actualResult = _aggregator.Data.Items.OrderBy(p => p, _comparer);
        actualResult.SequenceEqual(expectedResult).Should().Be(true);
    }

    public void Dispose()
    {
        _source.Dispose();
        _aggregator.Dispose();
        _virtualRequests.OnCompleted();
    }
}
