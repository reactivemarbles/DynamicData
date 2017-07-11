using System;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Cache
{
    
    public class ObservableToObservableChangeSetFixture
    {
        [Fact]
        public void OnNextFiresAdd()
        {
            var subject = new Subject<Person>();

            var results = subject.ToObservableChangeSet(p => p.Key).AsAggregator();
            var person = new Person("A", 1);
            subject.OnNext(person);

            results.Messages.Count.Should().Be(1, "Should be 1 updates");
            results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            results.Data.Items.First().Should().Be(person, "Should be same person");
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
            results.Data.Items.First().Should().Be(personamend, "Should be same person");
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
            results.Data.Items.First().Should().Be(person, "Should be same person");
        }

        [Fact]
        public void LimitSizeTo()
        {
            var subject = new Subject<Person>();
            var scheduler = new TestScheduler();
            var results = subject.ToObservableChangeSet(p => p.Key, limitSizeTo: 100, scheduler: scheduler).AsAggregator();

            var items = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();
            foreach (var person in items)
            {
                subject.OnNext(person);
            }

            scheduler.AdvanceBy(100000);

            results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(100, "Should be 100 removes");
            results.Data.Count.Should().Be(100);

            var expected = items.Skip(100).ToArray().OrderBy(p => p.Name).ToArray();
            var actual = results.Data.Items.OrderBy(p => p.Name).ToArray();
            actual.ShouldAllBeEquivalentTo(actual, "Only second hundred should be in the cache");
        }

        [Fact]
        public void ExpireAfterTime()
        {
            var subject = new Subject<Person>();
            var scheduler = new TestScheduler();
            var results = subject.ToObservableChangeSet(expireAfter: t => TimeSpan.FromMinutes(1), scheduler: scheduler).AsAggregator();

            var items = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();
            foreach (var person in items)
            {
                subject.OnNext(person);
            }

            scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);

            results.Messages.Count.Should().Be(201, "Should be 300 messages");
            results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(200, "Should be 100 removes");
            results.Data.Count.Should().Be(0, "Should be no data in the cache");
        }
    }
}
