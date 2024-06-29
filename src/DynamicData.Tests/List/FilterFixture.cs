using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class FilterFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public FilterFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect(p => p.Age > 20).AsAggregator();
    }

    [Fact]
    public void AddMatched()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void AddNotMatched()
    {
        var person = new Person("Adult1", 10);
        _source.Add(person);

        _results.Messages.Count.Should().Be(0, "Should have no item updates");
        _results.Data.Count.Should().Be(0, "Cache should have no items");
    }

    [Fact]
    public void AddNotMatchedAndUpdateMatched()
    {
        const string key = "Adult1";
        var notmatched = new Person(key, 19);
        var matched = new Person(key, 21);

        _source.Edit(
            list =>
            {
                list.Add(notmatched);
                list.Add(matched);
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].First().Range.First().Should().Be(matched, "Should be same person");
        _results.Data.Items[0].Should().Be(matched, "Should be same person");
    }

    [Fact]
    public void AddRange()
    {
        var itemstoadd = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToList();

        _source.AddRange(itemstoadd);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].First().Reason.Should().Be(ListChangeReason.AddRange, "Should be 1 updates");
        _results.Data.Count.Should().Be(80, "Should be 50 item in the cache");
    }

    [Fact]
    public void AddSubscribeRemove()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        var source = new SourceList<Person>();
        source.AddRange(people);

        var results = source.Connect(x => x.Age > 20).AsAggregator();
        source.RemoveMany(people.Where(x => x.Age % 2 == 0));

        results.Data.Count.Should().Be(40, "Should be 40 cached");
    }

    [Fact]
    public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
    {
        _source.Remove(new Person("anyone", 1));
        _results.Messages.Count.Should().Be(0, "Should be 0 updates");
    }

    [Fact]
    public void BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        _source.AddRange(people);
        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(80, "Should return 80 adds");

        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        _results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(filtered, "Incorrect Filter result");
    }

    [Fact]
    public void BatchRemoves()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        _source.AddRange(people);
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(80, "Should be 80 addes");
        _results.Messages[1].Removes.Should().Be(80, "Should be 80 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void BatchSuccessiveUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        foreach (var person in people)
        {
            var person1 = person;
            _source.Add(person1);
        }

        _results.Messages.Count.Should().Be(80, "Should be 80 updates");
        _results.Data.Count.Should().Be(80, "Should be 100 in the cache");
        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        _results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(filtered, "Incorrect Filter result");
    }

    [Fact]
    public void Clear()
    {
        var itemstoadd = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToList();

        _source.AddRange(itemstoadd);
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 1 updates");
        _results.Messages[0].First().Reason.Should().Be(ListChangeReason.AddRange, "First reason should be add range");
        _results.Messages[1].First().Reason.Should().Be(ListChangeReason.Clear, "Second reason should be clear");
        _results.Data.Count.Should().Be(0, "Should be 50 item in the cache");
    }

    [Fact]
    public void Clear1()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        _source.AddRange(people);
        _source.Clear();

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(80, "Should be 80 addes");
        _results.Messages[1].Removes.Should().Be(80, "Should be 80 removes");
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

        _source.Add(person);
        _source.Remove(person);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 80 addes");
        _results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void ReplaceWithMatch()
    {
        var itemstoadd = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToList();
        _source.AddRange(itemstoadd);

        _source.ReplaceAt(0, new Person("Adult1", 50));

        _results.Data.Count.Should().Be(81);
    }

    [Fact]
    public void ReplaceWithNonMatch()
    {
        var itemstoadd = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToList();
        _source.AddRange(itemstoadd);

        _source.ReplaceAt(50, new Person("Adult1", 1));

        _results.Data.Count.Should().Be(79);
    }

    [Fact]
    public void SameKeyChanges()
    {
        const string key = "Adult1";

        var toaddandremove = new Person(key, 53);
        _source.Edit(
            updater =>
            {
                updater.Add(new Person(key, 50));
                updater.Add(new Person(key, 52));
                updater.Add(toaddandremove);
                updater.Remove(toaddandremove);
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(3, "Should be 3 adds");
        _results.Messages[0].Replaced.Should().Be(0, "Should be 0 updates");
        _results.Messages[0].Removes.Should().Be(1, "Should be 1 remove");
    }

    [Fact]
    public void UpdateNotMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 10);
        var updated = new Person(key, 11);

        _source.Add(newperson);
        _source.Add(updated);

        _results.Messages.Count.Should().Be(0, "Should be no updates");
        _results.Data.Count.Should().Be(0, "Should nothing cached");
    }
}
