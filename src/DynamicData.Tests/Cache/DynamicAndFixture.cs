using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class DynamicAndFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceList<IObservable<IChangeSet<Person, string>>> _source;

    private readonly ISourceCache<Person, string> _source1;

    private readonly ISourceCache<Person, string> _source2;

    private readonly ISourceCache<Person, string> _source3;

    public DynamicAndFixture()
    {
        _source1 = new SourceCache<Person, string>(p => p.Name);
        _source2 = new SourceCache<Person, string>(p => p.Name);
        _source3 = new SourceCache<Person, string>(p => p.Name);
        _source = new SourceList<IObservable<IChangeSet<Person, string>>>();
        _results = _source.And().AsAggregator();
    }

    [Fact]
    public void AddAndRemoveLists()
    {
        var items = _generator.Take(100).ToArray();
        _source1.AddOrUpdate(items.Take(20));
        _source2.AddOrUpdate(items.Skip(10).Take(10));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        _results.Data.Count.Should().Be(10);
        _results.Data.Items.Should().BeEquivalentTo(items.Skip(10).Take(10));

        _source.Add(_source3.Connect());
        _results.Data.Count.Should().Be(0);

        _source.RemoveAt(2);
        _results.Data.Count.Should().Be(10);
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _source3.Dispose();
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void RemoveAllLists()
    {
        var items = _generator.Take(100).ToArray();

        _source1.AddOrUpdate(items.Take(10));
        _source2.AddOrUpdate(items.Skip(20).Take(10));
        _source3.AddOrUpdate(items.Skip(30));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        _source.RemoveAt(2);
        _source.RemoveAt(1);
        _source.RemoveAt(0);
        //s _source.Clear();

        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void RemovingFromOneRemovesFromResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        _source2.Remove(person);
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(0, "Cache should have no items");
    }

    [Fact]
    public void UpdatingBothProducesResults()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);
        _results.Messages.Count.Should().Be(1, "Should have no updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void UpdatingOneProducesOnlyOneUpdate()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        var personUpdated = new Person("Adult1", 51);
        _source2.AddOrUpdate(personUpdated);
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
        _results.Data.Items[0].Should().Be(personUpdated, "Should be updated person");
    }

    [Fact]
    public void UpdatingOneSourceOnlyProducesNoResults()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(0, "Should have no updates");
        _results.Data.Count.Should().Be(0, "Cache should have no items");
    }
}
