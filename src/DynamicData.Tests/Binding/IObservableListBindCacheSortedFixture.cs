using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class IObservableListBindCacheSortedFixture : IDisposable
{
    private static readonly IComparer<Person> _comparerAgeAscThanNameAsc = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

    private static readonly IComparer<Person> _comparerNameDesc = SortExpressionComparer<Person>.Descending(p => p.Name);

    private readonly BehaviorSubject<IComparer<Person>> _comparer = new(_comparerAgeAscThanNameAsc);

    private readonly RandomPersonGenerator _generator = new();

    private readonly IObservableList<Person> _list;

    private readonly ChangeSetAggregator<Person> _listNotifications;

    private readonly ISourceCache<Person, string> _source;

    private readonly SortedChangeSetAggregator<Person, string> _sourceCacheNotifications;

    public IObservableListBindCacheSortedFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _sourceCacheNotifications = _source.Connect().AutoRefresh().Sort(_comparer, resetThreshold: 10).BindToObservableList(out _list).AsAggregator();

        _listNotifications = _list.Connect().AsAggregator();
    }

    [Fact]
    public void AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        _list.Count.Should().Be(1, "Should be 1 item in the collection");
        _list.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void BatchAdd()
    {
        var people = _generator.Take(15).ToList();
        _source.AddOrUpdate(people);

        var sorted = people.OrderBy(p => p, _comparerAgeAscThanNameAsc).ToList();

        _list.Count.Should().Be(15, "Should be 15 items in the collection");
        _list.Items.Should().Equal(sorted, "Collections should be equivalent");
    }

    [Fact]
    public void BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        _list.Count.Should().Be(0, "Should be 0 items in the collection");
    }

    [Fact]
    public void CollectionIsInSortOrder()
    {
        _source.AddOrUpdate(_generator.Take(100));
        var sorted = _source.Items.OrderBy(p => p, _comparerAgeAscThanNameAsc).ToList();
        sorted.Should().Equal(_list.Items);
    }

    public void Dispose()
    {
        _sourceCacheNotifications.Dispose();
        _listNotifications.Dispose();
        _source.Dispose();
        _comparer.Dispose();
    }

    [Fact]
    public void InitialBindWithExistingData()
    {
        var source = new SourceCache<Person, string>(p => p.Name);

        // Populate source before binding
        var person1 = new Person("Adult1", 20);
        var person2 = new Person("Adult2", 30);
        source.AddOrUpdate(person2); // Add out of order to assert intial order
        source.AddOrUpdate(person1);

        var sourceCacheNotifications = source.Connect().AutoRefresh().Sort(_comparer, resetThreshold: 10).BindToObservableList(out var list).AsAggregator();

        var listNotifications = list.Connect().AsAggregator();

        // Assert
        listNotifications.Messages.Count.Should().Be(1);
        listNotifications.Messages.First().First().Reason.Should().Be(ListChangeReason.AddRange);
        list.Items.Should().Equal(person1, person2);

        // Clean up
        source.Dispose();
        sourceCacheNotifications.Dispose();
        listNotifications.Dispose();
        list.Dispose();
    }

    [Fact]
    public void ListRecievesMoves()
    {
        var person1 = new Person("Person1", 10);
        var person2 = new Person("Person2", 20);
        var person3 = new Person("Person3", 30);

        _source.AddOrUpdate(new Person[] { person1, person2, person3 });

        // Move person 3 to the front on the line
        person3.Age = 1;

        // 1 ChangeSet with AddRange & 1 ChangeSet with Refresh & Move
        _listNotifications.Messages.Count.Should().Be(2);

        // Assert AddRange
        var addChangeSet = _listNotifications.Messages.First();
        addChangeSet.First().Reason.Should().Be(ListChangeReason.AddRange);

        // Assert Refresh & Move
        var refreshAndMoveChangeSet = _listNotifications.Messages.Last();

        refreshAndMoveChangeSet.Count.Should().Be(2);

        var refreshChange = refreshAndMoveChangeSet.First();
        refreshChange.Reason.Should().Be(ListChangeReason.Refresh);
        refreshChange.Item.Current.Should().Be(person3);

        var moveChange = refreshAndMoveChangeSet.Last();
        moveChange.Reason.Should().Be(ListChangeReason.Moved);
        moveChange.Item.Current.Should().Be(person3);
        moveChange.Item.PreviousIndex.Should().Be(2);
        moveChange.Item.CurrentIndex.Should().Be(0);
    }

    [Fact]
    public void ListRecievesRefresh()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        person.Age = 60;

        _listNotifications.Messages.Count.Should().Be(2);
        _listNotifications.Messages.Last().First().Reason.Should().Be(ListChangeReason.Refresh);
    }

    [Fact]
    public void RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        _list.Count.Should().Be(0, "Should be 1 item in the collection");
    }

    [Fact]
    public void Reset()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray();

        _source.AddOrUpdate(people);

        _comparer.OnNext(_comparerNameDesc);

        var sorted = people.OrderBy(p => p, _comparerNameDesc).ToList();

        _list.Items.Should().Equal(sorted);

        _listNotifications.Messages.Count.Should().Be(2); // Initial loading change set and a reset change due to a change over the reset threshold.
        _listNotifications.Messages[0].First().Reason.Should().Be(ListChangeReason.AddRange); // initial loading
        _listNotifications.Messages[1].Count.Should().Be(2); // Reset
        _listNotifications.Messages[1].First().Reason.Should().Be(ListChangeReason.Clear); // reset
        _listNotifications.Messages[1].Last().Reason.Should().Be(ListChangeReason.AddRange); // reset
    }

    [Fact]
    public void TreatMovesAsRemoveAdd()
    {
        var cache = new SourceCache<Person, string>(p => p.Name);

        var people = Enumerable.Range(0, 10).Select(age => new Person("Person" + age, age)).ToList();
        var importantGuy = people.First();
        cache.AddOrUpdate(people);

        ISortedChangeSet<Person, string>? latestSetWithoutMoves = null;
        ISortedChangeSet<Person, string>? latestSetWithMoves = null;

        using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).TreatMovesAsRemoveAdd().BindToObservableList(out var boundList1).Subscribe(set => latestSetWithoutMoves = set))

        using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).BindToObservableList(out var boundList2).Subscribe(set => latestSetWithMoves = set))
        {
            importantGuy.Age += 200;

            if (latestSetWithoutMoves is null)
            {
                throw new InvalidOperationException(nameof(latestSetWithoutMoves));
            }

            if (latestSetWithMoves is null)
            {
                throw new InvalidOperationException(nameof(latestSetWithMoves));
            }

            latestSetWithoutMoves.Removes.Should().Be(1);
            latestSetWithoutMoves.Adds.Should().Be(1);
            latestSetWithoutMoves.Moves.Should().Be(0);
            latestSetWithoutMoves.Updates.Should().Be(0);

            latestSetWithMoves.Moves.Should().Be(1);
            latestSetWithMoves.Updates.Should().Be(0);
            latestSetWithMoves.Removes.Should().Be(0);
            latestSetWithMoves.Adds.Should().Be(0);
        }
    }

    [Fact]
    public void UpdateToSourceUpdatesTheDestination()
    {
        var person1 = new Person("Adult1", 20);
        var person2 = new Person("Adult2", 30);
        var personUpdated1 = new Person("Adult1", 40);

        _source.AddOrUpdate(person1);
        _source.AddOrUpdate(person2);

        _list.Items.Should().Equal(person1, person2);

        _source.AddOrUpdate(personUpdated1);

        _list.Items.Should().Equal(person2, personUpdated1);
    }
}
