using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using DynamicData.Aggregation;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class FilterWithObservable : IDisposable
{
    private readonly BehaviorSubject<Func<Person, bool>> _filter;

    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public FilterWithObservable()
    {
        _source = new SourceList<Person>();
        _filter = new BehaviorSubject<Func<Person, bool>>(p => p.Age > 20);
        _results = _source.Connect().Filter(_filter).AsAggregator();
    }

    /* Should be the same as standard lambda filter */

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
            updater =>
            {
                updater.Add(notmatched);
                updater.Add(matched);
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].First().Range.First().Should().Be(matched, "Should be same person");
        _results.Data.Items[0].Should().Be(matched, "Should be same person");
    }

    [Fact]
    public void AttemptedRemovalOfANonExistentKeyWillBeIgnored()
    {
        _source.Remove(new Person("A", 1));
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

        _results.Messages.Count.Should().Be(80, "Should be 80 messages");
        _results.Data.Count.Should().Be(80, "Should be 80 in the cache");
        var filtered = people.Where(p => p.Age > 20).OrderBy(p => p.Age).ToArray();
        _results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(filtered, "Incorrect Filter result");
    }

    [Fact]
    public void ChainFilters()
    {
        var filter2 = new BehaviorSubject<Func<Person, bool>>(person1 => person1.Age > 20);

        var stream = _source.Connect().Filter(_filter).Filter(filter2);

        var captureList = new List<int>();
        stream.Count().Subscribe(count => captureList.Add(count));

        var person = new Person("P", 30);
        _source.Add(person);

        person.Age = 10;
        _filter.OnNext(_filter.Value);

        captureList.Should().BeEquivalentTo(new[] { 1, 0 });
    }

    [Fact]
    public void ChangeFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToList();

        _source.AddRange(people);
        _results.Data.Count.Should().Be(80, "Should be 80 people in the cache");

        _filter.OnNext(p => p.Age <= 50);
        _results.Data.Count.Should().Be(50, "Should be 50 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

        _results.Data.Items.All(p => p.Age <= 50).Should().BeTrue();
    }

    [Fact]
    public void Clear()
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
        _filter.Dispose();
    }

    [Fact]
    public void ReevaluateFilter()
    {
        //re-evaluate for inline changes
        var people = Enumerable.Range(1, 100).Select(i => new Person("P" + i, i)).ToArray();

        _source.AddRange(people);
        _results.Data.Count.Should().Be(80, "Should be 80 people in the cache");

        foreach (var person in people)
        {
            person.Age += 10;
        }

        _filter.OnNext(_filter.Value);

        _results.Data.Count.Should().Be(90, "Should be 90 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");
        _results.Messages[1].Removes.Should().Be(0, "Should be 80 removes in the second message");
        _results.Messages[1].Adds.Should().Be(10, "Should be 10 adds in the second message");

        foreach (var person in people)
        {
            person.Age -= 10;
        }

        _filter.OnNext(_filter.Value);

        _results.Data.Count.Should().Be(80, "Should be 80 people in the cache");
        _results.Messages.Count.Should().Be(3, "Should be 3 update messages");
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
    public void RemoveFiltered()
    {
        var person = new Person("P1", 1);

        _source.Add(person);
        _results.Data.Count.Should().Be(0, "Should be 0 people in the cache");
        _filter.OnNext(p => p.Age >= 1);
        _results.Data.Count.Should().Be(1, "Should be 1 people in the cache");

        _source.Remove(person);

        _results.Data.Count.Should().Be(0, "Should be 0 people in the cache");
    }

    [Fact]
    public void RemoveFilteredRange()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("P" + i, i)).ToArray();

        _source.AddRange(people);
        _results.Data.Count.Should().Be(0, "Should be 0 people in the cache");
        _filter.OnNext(p => p.Age > 5);
        _results.Data.Count.Should().Be(5, "Should be 5 people in the cache");

        _source.RemoveRange(5, 5);

        _results.Data.Count.Should().Be(0, "Should be 0 people in the cache");
    }

    [Fact]
    public void SameKeyChanges()
    {
        const string key = "Adult1";

        _source.Edit(
            updater =>
            {
                updater.Add(new Person(key, 50));
                updater.Add(new Person(key, 52));
                updater.Add(new Person(key, 53));
                //    updater.Remove(key);
            });

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Messages[0].Adds.Should().Be(3, "Should be 3 adds");
    }

    [Fact]
    public void UpdateMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        _source.Add(newperson);
        _source.Replace(newperson, updated);

        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
        _results.Messages[1].Replaced.Should().Be(1, "Should be 1 update");
    }

    [Fact]
    public void UpdateNotMatched()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 10);
        var updated = new Person(key, 11);

        _source.Add(newperson);
        _source.Replace(newperson, updated);

        _results.Messages.Count.Should().Be(0, "Should be no updates");
        _results.Data.Count.Should().Be(0, "Should nothing cached");
    }
}
