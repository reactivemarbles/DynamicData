using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.Cache;

public class EnumerableObservableToObservableChangeSetFixture
{
    [Fact]
    public void ExpireAfterTime()
    {
        var subject = new Subject<IEnumerable<Person>>();
        var scheduler = new TestScheduler();
        var results = subject.ToObservableChangeSet<Person>(t => TimeSpan.FromMinutes(1), scheduler).AsAggregator();

        var people = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();

        subject.OnNext(people);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
        //scheduler.Start();
        results.Messages.Count.Should().Be(2, "Should be 300 messages");
        results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
        results.Messages.Sum(x => x.Removes).Should().Be(200, "Should be 100 removes");
        results.Data.Count.Should().Be(0, "Should be no data in the cache");
    }

    [Fact]
    public void LimitSizeTo()
    {
        var subject = new Subject<IEnumerable<Person>>();
        var scheduler = new TestScheduler();
        var results = subject.ToObservableChangeSet(100, scheduler).AsAggregator();

        var people = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();

        subject.OnNext(people);

        scheduler.AdvanceBy(1);

        results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
        results.Messages.Sum(x => x.Removes).Should().Be(100, "Should be 100 removes");
        results.Data.Count.Should().Be(100, "Should be 1 item in the cache");

        var expected = people.Skip(100).ToArray().OrderBy(p => p.Name).ToArray();
        var actual = results.Data.Items.OrderBy(p => p.Name).ToArray();
        actual.Should().BeEquivalentTo(expected, "Only second hundred should be in the cache");
    }

    [Fact]
    public void OnNextProducesAnAddAndRemoveChangeForEnumerableSource()
    {
        var subject = new Subject<IEnumerable<Person>>();

        var results = ObservableChangeSet.Create<Person, string>(cache =>
            {
                return subject.Subscribe(items => cache.EditDiff(items, Person.NameAgeGenderComparer));
            }, p => p.Name)
            .AsAggregator();


        var people = new[]
        {
            new Person("A", 1),
            new Person("B", 2),
            new Person("C", 3)
        };

        subject.OnNext(people);

        results.Messages.Last().Adds.Should().Be(3, "Should have added three items");
        results.Data.Count.Should().Be(3, "Should be 3 items in the cache");

        people = new[]
        {
            new Person("A", 3),
            new Person("B", 4)
        };

        subject.OnNext(people);

        results.Messages.Last().Adds.Should().Be(0, "Should have added no items");
        results.Messages.Last().Updates.Should().Be(2, "Should have updated 2 items");
         results.Messages.Last().Removes.Should().Be(1, "Should have removed 1 items");
        results.Data.Count.Should().Be(2, "Should be 3 items in the cache");

        results.Messages.Count.Should().Be(2, "Should be 2 updates");
        results.Data.Items.Should().BeEquivalentTo(results.Data.Items, "Lists should be equivalent");
    }

    [Fact]
    public void OnNextProducesAnAddChangeForEnumerableSource()
    {
        var subject = new Subject<IEnumerable<Person>>();
        var results = subject.ToObservableChangeSet().AsAggregator();

        var people = new[]
        {
            new Person("A", 1),
            new Person("B", 2),
            new Person("C", 3)
        };

        subject.OnNext(people);

        results.Messages.Count.Should().Be(1, "Should be 1 updates");
        results.Data.Count.Should().Be(3, "Should be 1 item in the cache");
        results.Data.Items.Should().BeEquivalentTo(results.Data.Items, "Lists should be equivalent");
    }



    [Fact]
    public void ExpireAfterObservableCompleted()
    {
        //See https://github.com/reactivemarbles/DynamicData/issues/358

        var scheduler = new TestScheduler();

        var expiry =  Observable.Return(Enumerable.Range(0, 10).Select(i => new { A = i, B = 2 * i }))
            .ToObservableChangeSet(x => x.A, _ => TimeSpan.FromSeconds(5), scheduler: scheduler)
            .AsAggregator();

        expiry.Data.Count.Should().Be(10);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);

        expiry.Data.Count.Should().Be(0);

    }
}
