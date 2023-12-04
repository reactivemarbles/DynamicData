using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;
using Xunit;

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


    [Fact]
    public void ResetThresholdsForBinding_ObservableCollection()
    {
        var people = _generator.Take(100).ToArray();



        // check whether reset is fired with different params
        var test1 = Test();
        var test2 = Test(new BindingOptions(95));
        var test3 = Test(new BindingOptions(105, ResetOnFirstTimeLoad: false));
        var test4 = Test(BindingOptions.NeverFireReset());


        test1.action.Should().Be(NotifyCollectionChangedAction.Reset);
        test2.action.Should().Be(NotifyCollectionChangedAction.Reset);
        test3.action.Should().Be(NotifyCollectionChangedAction.Add);
        test4.action.Should().Be(NotifyCollectionChangedAction.Add);

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

    [Fact]
    public void ResetThresholdsForBinding_ReadonlyObservableCollection()
    {
        var people = _generator.Take(100).ToArray();


        // check whether reset is fired with different params
        var test1 = Test();
        var test2 = Test(new BindingOptions(95));
        var test3 = Test(new BindingOptions(105, ResetOnFirstTimeLoad: false));
        var test4 = Test(BindingOptions.NeverFireReset());


        test1.action.Should().Be(NotifyCollectionChangedAction.Reset);
        test2.action.Should().Be(NotifyCollectionChangedAction.Reset);
        test3.action.Should().Be(NotifyCollectionChangedAction.Add);
        test4.action.Should().Be(NotifyCollectionChangedAction.Add);

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

    [Fact]
    public void AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        _collection.Count.Should().Be(1, "Should be 1 item in the collection");
        _collection.First().Should().Be(person, "Should be same person");
    }

    [Fact]
    public void BatchAdd()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        _collection.Count.Should().Be(100, "Should be 100 items in the collection");
        _collection.Should().BeEquivalentTo(_collection, "Collections should be equivalent");
    }

    [Fact]
    public void BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        _collection.Count.Should().Be(0, "Should be 100 items in the collection");
    }

    [Fact]
    public void CollectionIsInSortOrder()
    {
        _source.AddOrUpdate(_generator.Take(100));
        var sorted = _source.Items.OrderBy(p => p, _comparer).ToList();
        sorted.Should().BeEquivalentTo(_collection.ToList());
    }

    public void Dispose()
    {
        _binder.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void LargeUpdateInvokesAReset()
    {
        //update once as initial load is always a reset
        _source.AddOrUpdate(new Person("Me", 21));

        var invoked = false;
        _collection.CollectionChanged += (sender, e) =>
        {
            invoked = true;
            e.Action.Should().Be(NotifyCollectionChangedAction.Reset);
        };
        _source.AddOrUpdate(_generator.Take(100));

        invoked.Should().BeTrue();
    }

    [Fact]
    public void RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        _collection.Count.Should().Be(0, "Should be 1 item in the collection");
    }

    [Fact]
    public void SmallChangeDoesNotInvokeReset()
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

        invoked.Should().BeTrue();
        resetInvoked.Should().BeFalse();
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
    public void UpdateToSourceSendsRemoveAndAddIfSortingIsAffected()
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

        actions.Should().Equal(NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add);
        collection.Should().Equal(person2Updated, person1);
    }

    [Fact]
    public void UpdateToSourceSendsReplaceIfSortingIsNotAffected()
    {
        RunTest(true);
        RunTest(false);


        void RunTest(bool useReplace)
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
                action.Should().Be(NotifyCollectionChangedAction.Replace, "The notification type should be Replace");
            }
            else
            {
                action.Should().Be(NotifyCollectionChangedAction.Add, "The notification type should be Add");
            }

            collection.Should().Equal(person1, person2Updated);
        }
    }

    [Fact]
    public void UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(personUpdated);

        _collection.Count.Should().Be(1, "Should be 1 item in the collection");
        _collection.First().Should().Be(personUpdated, "Should be updated person");
    }
}
