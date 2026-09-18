#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding;

public class IObservableListBindCacheSortedFixture : IDisposable
{
    private static readonly IComparer<Person> _comparerAgeAscThanNameAsc = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

    private static readonly IComparer<Person> _comparerNameDesc = SortExpressionComparer<Person>.Descending(p => p.Name);

    private readonly ReactiveUI.Primitives.Signals.StateSignal<IComparer<Person>> _comparer = new(_comparerAgeAscThanNameAsc);

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

    [Test]
    public async Task AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_list.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_list.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task BatchAdd()
    {
        var people = _generator.Take(15).ToList();
        _source.AddOrUpdate(people);

        var sorted = people.OrderBy(p => p, _comparerAgeAscThanNameAsc).ToList();

        await Assert.That(_list.Count).IsEqualTo(15).Because("Should be 15 items in the collection");
        await Assert.That(_list.Items).IsEquivalentTo(sorted, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("Collections should be equivalent");
    }

    [Test]
    public async Task BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        await Assert.That(_list.Count).IsEqualTo(0).Because("Should be 0 items in the collection");
    }

    [Test]
    public async Task CollectionIsInSortOrder()
    {
        _source.AddOrUpdate(_generator.Take(100));
        var sorted = _source.Items.OrderBy(p => p, _comparerAgeAscThanNameAsc).ToList();
        await Assert.That(sorted).IsEquivalentTo(_list.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    public void Dispose()
    {
        _sourceCacheNotifications.Dispose();
        _listNotifications.Dispose();
        _source.Dispose();
        _comparer.Dispose();
    }

    [Test]
    public async Task InitialBindWithExistingData()
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
        await Assert.That(listNotifications.Messages.Count).IsEqualTo(1);
        await Assert.That(listNotifications.Messages.First().First().Reason).IsEqualTo(ListChangeReason.AddRange);
        await Assert.That(list.Items).IsEquivalentTo(new[] { person1, person2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        // Clean up
        source.Dispose();
        sourceCacheNotifications.Dispose();
        listNotifications.Dispose();
        list.Dispose();
    }

    [Test]
    public async Task ListRecievesMoves()
    {
        var person1 = new Person("Person1", 10);
        var person2 = new Person("Person2", 20);
        var person3 = new Person("Person3", 30);

        _source.AddOrUpdate(new Person[] { person1, person2, person3 });

        // Move person 3 to the front on the line
        person3.Age = 1;

        // 1 ChangeSet with AddRange & 1 ChangeSet with Refresh & Move
        await Assert.That(_listNotifications.Messages.Count).IsEqualTo(2);

        // Assert AddRange
        var addChangeSet = _listNotifications.Messages.First();
        await Assert.That(addChangeSet.First().Reason).IsEqualTo(ListChangeReason.AddRange);

        // Assert Refresh & Move
        var refreshAndMoveChangeSet = _listNotifications.Messages.Last();

        await Assert.That(refreshAndMoveChangeSet.Count).IsEqualTo(2);

        var refreshChange = refreshAndMoveChangeSet.First();
        await Assert.That(refreshChange.Reason).IsEqualTo(ListChangeReason.Refresh);
        await Assert.That(refreshChange.Item.Current).IsEqualTo(person3);

        var moveChange = refreshAndMoveChangeSet.Last();
        await Assert.That(moveChange.Reason).IsEqualTo(ListChangeReason.Moved);
        await Assert.That(moveChange.Item.Current).IsEqualTo(person3);
        await Assert.That(moveChange.Item.PreviousIndex).IsEqualTo(2);
        await Assert.That(moveChange.Item.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task ListRecievesRefresh()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        person.Age = 60;

        await Assert.That(_listNotifications.Messages.Count).IsEqualTo(2);
        await Assert.That(_listNotifications.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Refresh);
    }

    [Test]
    public async Task RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        await Assert.That(_list.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
    }

    [Test]
    public async Task Reset()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray();

        _source.AddOrUpdate(people);

        _comparer.OnNext(_comparerNameDesc);

        var sorted = people.OrderBy(p => p, _comparerNameDesc).ToList();

        await Assert.That(_list.Items).IsEquivalentTo(sorted, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await Assert.That(_listNotifications.Messages.Count).IsEqualTo(2); // Initial loading change set and a reset change due to a change over the reset threshold.
        await Assert.That(_listNotifications.Messages[0].First().Reason).IsEqualTo(ListChangeReason.AddRange); // initial loading
        await Assert.That(_listNotifications.Messages[1].Count).IsEqualTo(2); // Reset
        await Assert.That(_listNotifications.Messages[1].First().Reason).IsEqualTo(ListChangeReason.Clear); // reset
        await Assert.That(_listNotifications.Messages[1].Last().Reason).IsEqualTo(ListChangeReason.AddRange); // reset
    }

    [Test]
    public async Task TreatMovesAsRemoveAdd()
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

            await Assert.That(latestSetWithoutMoves.Removes).IsEqualTo(1);
            await Assert.That(latestSetWithoutMoves.Adds).IsEqualTo(1);
            await Assert.That(latestSetWithoutMoves.Moves).IsEqualTo(0);
            await Assert.That(latestSetWithoutMoves.Updates).IsEqualTo(0);

            await Assert.That(latestSetWithMoves.Moves).IsEqualTo(1);
            await Assert.That(latestSetWithMoves.Updates).IsEqualTo(0);
            await Assert.That(latestSetWithMoves.Removes).IsEqualTo(0);
            await Assert.That(latestSetWithMoves.Adds).IsEqualTo(0);
        }
    }

    [Test]
    public async Task UpdateToSourceUpdatesTheDestination()
    {
        var person1 = new Person("Adult1", 20);
        var person2 = new Person("Adult2", 30);
        var personUpdated1 = new Person("Adult1", 40);

        _source.AddOrUpdate(person1);
        _source.AddOrUpdate(person2);

        await Assert.That(_list.Items).IsEquivalentTo(new[] { person1, person2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        _source.AddOrUpdate(personUpdated1);

        await Assert.That(_list.Items).IsEquivalentTo(new[] { person2, personUpdated1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
