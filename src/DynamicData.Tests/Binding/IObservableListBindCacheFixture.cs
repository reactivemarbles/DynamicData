using System;
using System.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class IObservableListBindCacheFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly IObservableList<Person> _list;

    private readonly ChangeSetAggregator<Person> _listNotifications;

    private readonly ISourceCache<Person, string> _source;

    private readonly ChangeSetAggregator<Person, string> _sourceCacheNotifications;

    public IObservableListBindCacheFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _sourceCacheNotifications = _source.Connect().AutoRefresh().BindToObservableList(out _list).AsAggregator();

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
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);

        _list.Count.Should().Be(100, "Should be 100 items in the collection");
        _list.Should().BeEquivalentTo(_list, "Collections should be equivalent");
    }

    [Fact]
    public void BatchRemove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddOrUpdate(people);
        _source.Clear();
        _list.Count.Should().Be(0, "Should be 100 items in the collection");
    }

    public void Dispose()
    {
        _sourceCacheNotifications.Dispose();
        _listNotifications.Dispose();
        _source.Dispose();
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
    public void UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.AddOrUpdate(person);
        _source.AddOrUpdate(personUpdated);

        _list.Count.Should().Be(1, "Should be 1 item in the collection");
        _list.Items[0].Should().Be(personUpdated, "Should be updated person");
    }
}
