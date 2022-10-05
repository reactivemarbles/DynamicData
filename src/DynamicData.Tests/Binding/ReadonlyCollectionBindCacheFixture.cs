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

public class ReadonlyCollectionBindCacheFixture : IDisposable
{
    private readonly IDisposable _binder;
    private readonly ReadOnlyObservableCollection<Person> _collection;
    private readonly RandomPersonGenerator _generator = new();
    private readonly ISourceCache<Person, string> _source;

    public ReadonlyCollectionBindCacheFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _binder = _source.Connect().Bind(out _collection).Subscribe();
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
            using var source = new SourceCache<Person, string>(p => p.Name);
            using var binder = source.Connect().Bind(out var collection, useReplaceForUpdates: useReplace).Subscribe();

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
