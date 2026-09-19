#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding;

public class ObservableCollectionBindCacheSortedFixture : IDisposable
{
    private readonly IDisposable _binder;

    private readonly ObservableCollectionExtended<Person> _collection;

    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name);

    private readonly RandomPersonGenerator _generator = new();

    private readonly ISourceCache<Person, string> _source;

    public ObservableCollectionBindCacheSortedFixture()
    {
        _collection = new ObservableCollectionExtended<Person>();
        _source = new SourceCache<Person, string>(p => p.Name);
        _binder = _source.Connect().Sort(_comparer, resetThreshold: 25).Bind(_collection).Subscribe();
    }

    [Test]
    public async Task ResetThresholdsForBinding_ObservableCollection()
    {
        var people = _generator.Take(100).ToArray();

        // check whether reset is fired with different params
        var test1 = Test();
        var test2 = Test(new BindingOptions(95));
        var test3 = Test(new BindingOptions(105, ResetOnFirstTimeLoad: false));
        var test4 = Test(BindingOptions.NeverFireReset());

        await Assert.That(test1.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test2.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test3.action).IsEqualTo(NotifyCollectionChangedAction.Add);
        await Assert.That(test4.action).IsEqualTo(NotifyCollectionChangedAction.Add);

        return;

        (NotifyCollectionChangedAction action, ObservableCollectionExtended<Person> list) Test(BindingOptions? options = null)
        {
            _source.Clear();

            NotifyCollectionChangedAction? result = null;

            var list = new ObservableCollectionExtended<Person>();
            using var listEvents = list.ObserveCollectionChanges().Take(1)
                .Select(e => e.EventArgs.Action)
                .Subscribe(events =>
                {
                    result = events;
                });

            var binder = options == null
                ? _source.Connect().Sort(_comparer).Bind(list).Subscribe()
                : _source.Connect().Sort(_comparer).Bind(list, options.Value).Subscribe();

            _source.AddOrUpdate(people);
            binder.Dispose();

            return (result!.Value, list);
        }
    }

    [Test]
    public async Task ResetThresholdsForBinding_ReadonlyObservableCollection()
    {
        var people = _generator.Take(100).ToArray();

        // check whether reset is fired with different params
        var test1 = Test();
        var test2 = Test(new BindingOptions(95));
        var test3 = Test(new BindingOptions(105, ResetOnFirstTimeLoad: false));
        var test4 = Test(BindingOptions.NeverFireReset());

        await Assert.That(test1.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test2.action).IsEqualTo(NotifyCollectionChangedAction.Reset);
        await Assert.That(test3.action).IsEqualTo(NotifyCollectionChangedAction.Add);
        await Assert.That(test4.action).IsEqualTo(NotifyCollectionChangedAction.Add);

        return;

        (NotifyCollectionChangedAction action, ReadOnlyObservableCollection<Person> list) Test(BindingOptions? options = null)
        {
            _source.Clear();

            NotifyCollectionChangedAction? result = null;
            ReadOnlyObservableCollection<Person> list;
            //var list = new ObservableCollectionExtended<Person>();

            var binder = options == null
                ? _source.Connect().Sort(_comparer).Bind(out list).Subscribe()
                : _source.Connect().Sort(_comparer).Bind(out list, options.Value).Subscribe();

            using var listEvents = list.ObserveCollectionChanges().Take(1)
                .Select(e => e.EventArgs.Action)
                .Subscribe(events =>
                {
                    result = events;
                });

            _source.AddOrUpdate(people);
            binder.Dispose();
            return (result!.Value, list);
        }
    }

    [Test]
    public async Task AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task BatchAdd()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        await Assert.That(_collection.Count).IsEqualTo(100).Because("Should be 100 items in the collection");
        await Assert.That(_collection).IsEquivalentTo(_collection).Because("Collections should be equivalent");
    }

    [Test]
    public async Task BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 100 items in the collection");
    }

    [Test]
    public async Task CollectionIsInSortOrder()
    {
        _source.AddOrUpdate(_generator.Take(100));
        var sorted = _source.Items.OrderBy(p => p, _comparer).ToList();
        await Assert.That(sorted).IsEquivalentTo(_collection.ToList());
    }

    public void Dispose()
    {
        _binder.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task LargeUpdateInvokesAReset()
    {
        //update once as initial load is always a reset
        _source.AddOrUpdate(new Person("Me", 21));

        var invoked = false;
        NotifyCollectionChangedAction? action = null;
        _collection.CollectionChanged += (sender, e) =>
        {
            invoked = true;
            action = e.Action;
        };
        _source.AddOrUpdate(_generator.Take(100));

        await Assert.That(invoked).IsTrue();
        await Assert.That(action).IsEqualTo(NotifyCollectionChangedAction.Reset);
    }

    [Test]
    public async Task RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        await Assert.That(_collection.Count).IsEqualTo(0).Because("Should be 1 item in the collection");
    }

    [Test]
    public async Task SmallChangeDoesNotInvokeReset()
    {
        //update once as initial load is always a reset
        _source.AddOrUpdate(new Person("Me", 21));

        var invoked = false;
        var resetInvoked = false;
        _collection.CollectionChanged += (sender, e) =>
        {
            invoked = true;
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                resetInvoked = true;
            }
        };
        _source.AddOrUpdate(_generator.Take(24));

        await Assert.That(invoked).IsTrue();
        await Assert.That(resetInvoked).IsFalse();
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

        var boundList1 = new ObservableCollectionExtended<Person>();
        var boundList2 = new ObservableCollectionExtended<Person>();

        using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).TreatMovesAsRemoveAdd().Bind(boundList1).Subscribe(set => latestSetWithoutMoves = set))

        using (cache.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Bind(boundList2).Subscribe(set => latestSetWithMoves = set))
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
    public async Task UpdateToSourceSendsRemoveAndAddIfSortingIsAffected()
    {
        var person1 = new Person("Adult1", 10);
        var person2 = new Person("Adult2", 11);
        var person2Updated = new Person("Adult2", 1);

        var actions = new List<NotifyCollectionChangedAction>();
        var collection = new ObservableCollectionExtended<Person>();

        using (var source = new SourceCache<Person, string>(person => person.Name))
        using (source.Connect().Sort(SortExpressionComparer<Person>.Ascending(person => person.Age)).Bind(collection).Subscribe())
        {
            source.AddOrUpdate(person1);
            source.AddOrUpdate(person2);

            using (collection.ObserveCollectionChanges().Select(change => change.EventArgs.Action).Subscribe(act => actions.Add(act)))
            {
                source.AddOrUpdate(person2Updated);
            }
        }

        await Assert.That(actions).IsEquivalentTo(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(collection).IsEquivalentTo(new[] { person2Updated, person1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task UpdateToSourceSendsReplaceIfSortingIsNotAffected()
    {
        await RunTest(true);
        await RunTest(false);

        async Task RunTest(bool useReplace)
        {
            var collection = new ObservableCollectionExtended<Person>();

            using var source = new SourceCache<Person, string>(p => p.Name);
            using var binder = source.Connect().Sort(_comparer, resetThreshold: 25).Bind(collection, new ObservableCollectionAdaptor<Person, string>(useReplaceForUpdates: useReplace)).Subscribe();

            var person1 = new Person("Adult1", 10);
            var person2 = new Person("Adult2", 11);

            NotifyCollectionChangedAction action = default;
            source.AddOrUpdate(person1);
            source.AddOrUpdate(person2);

            var person2Updated = new Person("Adult2", 12);

            using (collection.ObserveCollectionChanges().Select(x => x.EventArgs.Action).Subscribe(updateType => action = updateType))
            {
                source.AddOrUpdate(person2Updated);
            }

            if (useReplace)
            {
                await Assert.That(action).IsEqualTo(NotifyCollectionChangedAction.Replace).Because("The notification type should be Replace");
            }
            else
            {
                await Assert.That(action).IsEqualTo(NotifyCollectionChangedAction.Add).Because("The notification type should be Add");
            }

            await Assert.That(collection).IsEquivalentTo(new[] { person1, person2Updated }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(personUpdated);

        await Assert.That(_collection.Count).IsEqualTo(1).Because("Should be 1 item in the collection");
        await Assert.That(_collection.First()).IsEqualTo(personUpdated).Because("Should be updated person");
    }
}
