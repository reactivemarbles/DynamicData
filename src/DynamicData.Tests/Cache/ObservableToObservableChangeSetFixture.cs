using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Cache;

public class ObservableToObservableChangeSetFixture
{

    [Fact]
    public void ExpireAfterTime()
    {
        var subject = new Subject<Person>();
        var scheduler = new TestScheduler();
        var results = subject.ToObservableChangeSet(t => TimeSpan.FromMinutes(1), scheduler).AsAggregator();

        var items = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();
        foreach (var person in items)
        {
            subject.OnNext(person);
        }

        scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);

        results.Messages.Count.Should().Be(201, "Should be 201 messages");
        results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
        results.Messages.Sum(x => x.Removes).Should().Be(200, "Should be 200 removes");
        results.Data.Count.Should().Be(0, "Should be no data in the cache");
    }

    [Fact]
    public void ExpireAfterTimeDynamic()
    {
        var scheduler = new TestScheduler();
        var source = Observable.Interval(TimeSpan.FromSeconds(1), scheduler).Take(30).Select(i => (int)i).Select(i => new Person("p" + i.ToString("000"), i));

        var results = source.ToObservableChangeSet(t => TimeSpan.FromSeconds(10), scheduler).AsAggregator();

        scheduler.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);

        results.Messages.Count.Should().Be(50, "Should be 50 messages");
        results.Messages.Sum(x => x.Adds).Should().Be(30, "Should be 30 adds");
        results.Messages.Sum(x => x.Removes).Should().Be(20, "Should be 20 removes");
        results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
    }


    [Fact]
    public void ExpireAfterTimeDynamicWithKey()
    {
        var scheduler = new TestScheduler();
        var source = Observable.Interval(TimeSpan.FromSeconds(1), scheduler).Take(30).Select(i => (int)i).Select(i => new Person("p" + i.ToString("000"), i));

        var results = source.ToObservableChangeSet(p => p.Key, t => TimeSpan.FromSeconds(10), scheduler: scheduler).AsAggregator();

        scheduler.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);

        results.Messages.Count.Should().Be(50, "Should be 50 messages");
        results.Messages.Sum(x => x.Adds).Should().Be(30, "Should be 30 adds");
        results.Messages.Sum(x => x.Removes).Should().Be(20, "Should be 20 removes");
        results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
    }

    [Fact]
    public void ExpireAfterTimeWithKey()
    {
        var subject = new Subject<Person>();
        var scheduler = new TestScheduler();
        var results = subject.ToObservableChangeSet(p => p.Key, t => TimeSpan.FromMinutes(1), scheduler: scheduler).AsAggregator();

        var items = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();
        foreach (var person in items)
        {
            subject.OnNext(person);
        }

        results.Data.Count.Should().Be(200, "Should 200 items in the cache");

        scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);

        results.Messages.Count.Should().Be(201, "Should be 201 messages");
        results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
        results.Messages.Sum(x => x.Removes).Should().Be(200, "Should be 200 removes");
        results.Data.Count.Should().Be(0, "Should be no data in the cache");
    }

    [Fact]
    public void LimitSizeTo()
    {
        var subject = new Subject<Person>();
        var scheduler = new TestScheduler();
        var results = subject.ToObservableChangeSet(p => p.Key, limitSizeTo: 100, scheduler: scheduler).AsAggregator();

        var items = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();

        items.ForEach(subject.OnNext);

        scheduler.Start();

        results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
        results.Messages.Sum(x => x.Removes).Should().Be(100, "Should be 100 removes");
        results.Data.Count.Should().Be(100);

        var expected = items.Skip(100).ToArray().OrderBy(p => p.Name).ToArray();
        var actual = results.Data.Items.OrderBy(p => p.Name).ToArray();
        expected.Should().BeEquivalentTo(actual, "Only second hundred should be in the cache");
    }

    [Fact]
    public void OnNextFiresAdd()
    {
        var subject = new Subject<Person>();

        var results = subject.ToObservableChangeSet(p => p.Key).AsAggregator();
        var person = new Person("A", 1);
        subject.OnNext(person);

        results.Messages.Count.Should().Be(1, "Should be 1 updates");
        results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void OnNextForAmendedItemFiresUpdate()
    {
        var subject = new Subject<Person>();

        var results = subject.ToObservableChangeSet(p => p.Key).AsAggregator();
        var person = new Person("A", 1);
        subject.OnNext(person);

        var personamend = new Person("A", 2);
        subject.OnNext(personamend);

        results.Messages.Count.Should().Be(2, "Should be 2 message");
        results.Messages[1].Updates.Should().Be(1, "Should be 1 updates");
        results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        results.Data.Items[0].Should().Be(personamend, "Should be same person");
    }

    [Fact]
    public void OnNextProducesAndAddChangeForSingleItem()
    {
        var subject = new Subject<Person>();

        var results = subject.ToObservableChangeSet(p => p.Key).AsAggregator();
        var person = new Person("A", 1);
        subject.OnNext(person);

        results.Messages.Count.Should().Be(1, "Should be 1 updates");
        results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        results.Data.Items[0].Should().Be(person, "Should be same person");
    }
}
