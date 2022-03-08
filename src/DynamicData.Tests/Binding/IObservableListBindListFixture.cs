using System;
using System.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class IObservableListBindListFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly IObservableList<Person> _list;

    private readonly ChangeSetAggregator<Person> _observableListNotifications;

    private readonly SourceList<Person> _source;

    private readonly ChangeSetAggregator<Person> _sourceListNotifications;

    public IObservableListBindListFixture()
    {
        _source = new SourceList<Person>();
        _sourceListNotifications = _source.Connect().AutoRefresh().BindToObservableList(out _list).AsAggregator();

        _observableListNotifications = _list.Connect().AsAggregator();
    }

    [Fact]
    public void AddRange()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _list.Count.Should().Be(100, "Should be 100 items in the collection");
        _list.Should().BeEquivalentTo(_list, "Collections should be equivalent");
    }

    [Fact]
    public void AddToSourceAddsToDestination()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        _list.Count.Should().Be(1, "Should be 1 item in the collection");
        _list.Items.First().Should().Be(person, "Should be same person");
    }

    [Fact]
    public void Clear()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);
        _source.Clear();
        _list.Count.Should().Be(0, "Should be 100 items in the collection");
    }

    public void Dispose()
    {
        _sourceListNotifications.Dispose();
        _observableListNotifications.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void ListRecievesRefresh()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        person.Age = 60;

        _observableListNotifications.Messages.Count().Should().Be(2);
        _observableListNotifications.Messages.Last().First().Reason.Should().Be(ListChangeReason.Refresh);
    }

    [Fact]
    public void RemoveSourceRemovesFromTheDestination()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);
        _source.Remove(person);

        _list.Count.Should().Be(0, "Should be 1 item in the collection");
    }

    [Fact]
    public void UpdateToSourceUpdatesTheDestination()
    {
        var person = new Person("Adult1", 50);
        var personUpdated = new Person("Adult1", 51);
        _source.Add(person);
        _source.Replace(person, personUpdated);

        _list.Count.Should().Be(1, "Should be 1 item in the collection");
        _list.Items.First().Should().Be(personUpdated, "Should be updated person");
    }
}
