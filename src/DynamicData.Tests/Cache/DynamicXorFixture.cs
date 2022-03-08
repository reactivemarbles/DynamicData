using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class DynamicXorFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceList<IObservable<IChangeSet<Person, string>>> _source;

    private readonly ISourceCache<Person, string> _source1;

    private readonly ISourceCache<Person, string> _source2;

    private readonly ISourceCache<Person, string> _source3;

    public DynamicXorFixture()
    {
        _source1 = new SourceCache<Person, string>(p => p.Name);
        _source2 = new SourceCache<Person, string>(p => p.Name);
        _source3 = new SourceCache<Person, string>(p => p.Name);
        _source = new SourceList<IObservable<IChangeSet<Person, string>>>();
        _results = _source.Xor().AsAggregator();
    }

    [Fact]
    public void AddAndRemoveLists()
    {
        var items = _generator.Take(100).ToArray();

        _source1.AddOrUpdate(items.Take(10));
        _source2.AddOrUpdate(items.Skip(10).Take(10));
        _source3.AddOrUpdate(items.Skip(20));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        _results.Data.Count.Should().Be(100);
        _results.Data.Items.Should().BeEquivalentTo(items);

        _source.RemoveAt(1);

        var result = items.Take(10).Union(items.Skip(20));
        _results.Data.Count.Should().Be(90);
        _results.Data.Items.Should().BeEquivalentTo(result);
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
        _source2.AddOrUpdate(items.Skip(10).Take(10));
        _source3.AddOrUpdate(items.Skip(20));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        _source.Clear();

        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void RemovingFromOneDoesNotFromResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        _source2.Remove(person);
        _results.Messages.Count.Should().Be(3, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
    }

    [Fact]
    public void UpdatingBothDoeNotProducesResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);
        _results.Data.Count.Should().Be(0, "Cache should have no items");
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
        _results.Data.Count.Should().Be(0, "Cache should have no items");
    }

    [Fact]
    public void UpdatingOneSourceOnlyProducesResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());

        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
    }
}
