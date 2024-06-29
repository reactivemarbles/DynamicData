using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class GroupOnFixture : IDisposable
{
    private readonly ChangeSetAggregator<IGroup<Person, int>> _results;

    private readonly ISourceList<Person> _source;

    public GroupOnFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().GroupOn(p => p.Age).AsAggregator();
    }

    [Fact]
    public void Add()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        var firstGroup = _results.Data.Items[0].List.Items.ToArray();
        firstGroup[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void BigList()
    {
        var generator = new RandomPersonGenerator();
        var people = generator.Take(10000).ToArray();
        _source.AddRange(people);

        Console.WriteLine();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void Remove()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);
        _source.Remove(person);
        _results.Messages.Count.Should().Be(2, "Should be 1 updates");
        _results.Data.Count.Should().Be(0, "Should be no groups");
    }

    [Fact]
    public void UpdateWillChangeTheGroup()
    {
        var person = new Person("Adult1", 50);
        var amended = new Person("Adult1", 60);
        _source.Add(person);
        _source.ReplaceAt(0, amended);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        var firstGroup = _results.Data.Items[0].List.Items.ToArray();
        firstGroup[0].Should().Be(amended, "Should be same person");
    }
}
