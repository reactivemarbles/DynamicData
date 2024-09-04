using System;
using System.Linq;

using DynamicData.PLinq;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformFixtureParallel : IDisposable
{
    private readonly Func<Person, PersonWithGender> _transformFactory = p =>
    {
        var gender = p.Age % 2 == 0 ? "M" : "F";
        return new PersonWithGender(p, gender);
    };

    private readonly ChangeSetAggregator<PersonWithGender, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public TransformFixtureParallel()
    {
        _source = new SourceCache<Person, string>(p => p.Name);

        var pTransform = _source.Connect().Transform(_transformFactory, new ParallelisationOptions(ParallelType.Parallelise));
        _results = new ChangeSetAggregator<PersonWithGender, string>(pTransform);
    }

    [Fact]
    public void Add()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(_transformFactory(person), "Should be same person");
    }

    [Fact]
    public void BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        _source.AddOrUpdate(people);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(100, "Should return 100 adds");

        var transformed = people.Select(_transformFactory).ToArray();

        _results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(transformed, "Incorrect transform result");
    }

    [Fact]
    public void Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddOrUpdate(people);

        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(100, "Should be 80 addes");
        _results.Messages[1].Removes.Should().Be(100, "Should be 80 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        _source.AddOrUpdate(person);
        _source.Remove(key);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 80 addes");
        _results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void SameKeyChanges()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();
        _source.AddOrUpdate(people);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should return 1 adds");
        _results.Messages[0].Updates.Should().Be(9, "Should return 9 adds");
        _results.Data.Count.Should().Be(1, "Should result in 1 record");

        var lastTransformed = _transformFactory(people.Last());
        var onlyItemInCache = _results.Data.Items[0];

        // TODO: This is not producing consitent results, the lastTransformed item should be equal to the onlyItemInCache
        onlyItemInCache.Should().Be(lastTransformed, "Incorrect transform result");
    }

    [Fact]
    public void Update()
    {
        const string key = "Adult1";
        var newPerson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.AddOrUpdate(newPerson);
        _source.AddOrUpdate(updated);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
        _results.Messages[1].Updates.Should().Be(1, "Should be 1 update");
    }
}
