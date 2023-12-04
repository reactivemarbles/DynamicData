using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class ObservableCollectionBindCacheFixture : IDisposable
{
    private readonly IDisposable _binder;

    private readonly ObservableCollectionExtended<Person> _collection = new();

    private readonly RandomPersonGenerator _generator = new();

    private readonly ISourceCache<Person, string> _source;

    public ObservableCollectionBindCacheFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _binder = _source.Connect().Bind(_collection).Subscribe();
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
                ? _source.Connect().Bind(list).Subscribe()
                : _source.Connect().Bind(list, options.Value).Subscribe();

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
                ? _source.Connect().Bind(out list).Subscribe()
                : _source.Connect().Bind(out list, options.Value).Subscribe();

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

    public void Dispose()
    {
        _binder.Dispose();
        _source.Dispose();
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
    public void UpdateToSourceSendsReplaceOnDestination()
    {
        RunTest(true);
        RunTest(false);


        void RunTest(bool useReplace)
        {
            var collection = new ObservableCollectionExtended<Person>();

            using var source =  new SourceCache<Person, string>(p => p.Name);
            using var binder = source.Connect().Bind(collection, new ObservableCollectionAdaptor<Person, string>(useReplaceForUpdates: useReplace)).Subscribe();


            NotifyCollectionChangedAction action = default;
            source.AddOrUpdate(new Person("Adult1", 50));

            using (collection.ObserveCollectionChanges().Select(x => x.EventArgs.Action).Subscribe(updateType => action = updateType))
            {
                source.AddOrUpdate(new Person("Adult1", 51));
            }

            if (useReplace)
            {
                action.Should().Be(NotifyCollectionChangedAction.Replace);
            }
            else
            {
                action.Should().Be(NotifyCollectionChangedAction.Add);
            }
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
