using System;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class FilterFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public FilterFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = _source.Connect(p => p.Age > 20).AsAggregator();
    }

    [Fact]
    public void AddMatched()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void AddNotMatched()
    {
        var person = new Person("Adult1", 10);
        _source.AddOrUpdate(person);

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
            updater =>
            {
                updater.AddOrUpdate(notmatched);
                updater.AddOrUpdate(matched);
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].First().Current.Should().Be(matched, "Should be same person");
        _results.Data.Items[0].Should().Be(matched, "Should be same person");
    }

    [Fact]
    public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
    {
        const string key = "Adult1";
        _source.Remove(key);
        _results.Messages.Count.Should().Be(0, "Should be 0 updates");
    }

    [Fact]
    public void BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        _source.AddOrUpdate(people);
        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(80, "Should return 80 adds");

        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        var expected = _results.Data.Items.OrderBy(p => p.Age).ToArray();
        expected.Should().BeEquivalentTo(filtered, "Incorrect Filter result");
    }

    [Fact]
    public void BatchRemoves()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        _source.AddOrUpdate(people);
        _source.Remove(people);

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
            _source.AddOrUpdate(person1);
        }

        _results.Messages.Count.Should().Be(80, "Should be 100 updates");
        _results.Data.Count.Should().Be(80, "Should be 100 in the cache");
        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        _results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(filtered, "Incorrect Filter result");
    }

    [Fact]
    public void Clear()
    {
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
        _source.AddOrUpdate(people);
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
    public void DuplicateKeyWithMerge()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 30);

        using var results = _source.Connect().Merge(_source.Connect()).Filter(p => p.Age > 20).AsAggregator();
        _source.AddOrUpdate(newperson); // previously this would throw an exception

        results.Messages.Count.Should().Be(2, "Should be 2 messages");
        results.Messages[0].Adds.Should().Be(1, "Should be 1 add");
        results.Messages[1].Updates.Should().Be(1, "Should be 1 update");
        results.Data.Count.Should().Be(1, "Should be cached");
    }

    [Fact]
    public void Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        _source.AddOrUpdate(person);
        _source.Remove(person);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 80 adds");
        _results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        _results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void SameKeyChanges()
    {
        const string key = "Adult1";

        _source.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person(key, 50));
                updater.AddOrUpdate(new Person(key, 52));
                updater.AddOrUpdate(new Person(key, 53));
                updater.Remove(key);
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
        _results.Messages[0].Updates.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Removes.Should().Be(1, "Should be 1 remove");
    }

    [Fact]
    public void UpdateMatched()

    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);
        _source.AddOrUpdate(newperson);
        _source.AddOrUpdate(updated);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
        _results.Messages[1].Updates.Should().Be(1, "Should be 1 update");
    }

    [Fact]
    public void UpdateNotMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 10);
        var updated = new Person(key, 11);

        _source.AddOrUpdate(newperson);
        _source.AddOrUpdate(updated);

        _results.Messages.Count.Should().Be(0, "Should be no updates");
        _results.Data.Count.Should().Be(0, "Should nothing cached");
    }

    [Fact]
    public void EmptyChanges()
    {
        IChangeSet<Person, string>? change = null;
            
        //need to also apply overload on connect as that will also need to provide and empty notification
        // [alternatively _source.Connect(x=> x.Age == 20, suppressEmptyChangeSets: false)] instead 
        using var subscription = _source.Connect(suppressEmptyChangeSets: false)
            .Filter(x=> x.Age == 20, false)
            .Subscribe(c => change = c);

        change.Should().NotBeNull();
        change!.Count.Should().Be(0);
    }
}
