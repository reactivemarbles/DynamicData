using System;
using System.Linq;

using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class GroupOnPropertyWithImmutableStateFixture : IDisposable
{
    private readonly ChangeSetAggregator<IGrouping<Person, string, int>, int> _results;

    private readonly SourceCache<Person, string> _source;

    public GroupOnPropertyWithImmutableStateFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = _source.Connect().GroupOnPropertyWithImmutableState(p => p.Age).AsAggregator();
    }

    [Fact]
    public void CanGroupOnAdds()
    {
        _source.AddOrUpdate(new Person("A", 10));

        _results.Data.Count.Should().Be(1);

        var firstGroup = _results.Data.Items[0];

        firstGroup.Count.Should().Be(1);
        firstGroup.Key.Should().Be(10);
    }

    [Fact]
    public void CanHandleAddBatch()
    {
        var generator = new RandomPersonGenerator();
        var people = generator.Take(1000).ToArray();

        _source.AddOrUpdate(people);

        var expectedGroupCount = people.Select(p => p.Age).Distinct().Count();
        _results.Data.Count.Should().Be(expectedGroupCount);
    }

    [Fact]
    public void CanHandleChangedItemsBatch()
    {
        var generator = new RandomPersonGenerator();
        var people = generator.Take(100).ToArray();

        _source.AddOrUpdate(people);

        var initialCount = people.Select(p => p.Age).Distinct().Count();
        _results.Data.Count.Should().Be(initialCount);

        people.Take(25).ForEach(p => p.Age = 200);

        var changedCount = people.Select(p => p.Age).Distinct().Count();
        _results.Data.Count.Should().Be(changedCount);

        //check that each item is only in one cache
        var peopleInCache = _results.Data.Items.SelectMany(g => g.Items).ToArray();

        peopleInCache.Length.Should().Be(100);
    }

    [Fact]
    public void CanRemoveFromGroup()
    {
        var person = new Person("A", 10);
        _source.AddOrUpdate(person);
        _source.Remove(person);

        _results.Data.Count.Should().Be(0);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void Regroup()
    {
        var person = new Person("A", 10);
        _source.AddOrUpdate(person);
        person.Age = 20;

        _results.Data.Count.Should().Be(1);
        var firstGroup = _results.Data.Items[0];

        firstGroup.Count.Should().Be(1);
        firstGroup.Key.Should().Be(20);
    }
}
