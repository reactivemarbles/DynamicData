using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Cache
{
    
    public class EnumerableObservableToObservableChangeSetFixture
    {
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
            results.Data.Items.ShouldAllBeEquivalentTo(results.Data.Items, "Lists should be equivalent");
        }

        [Fact]
        public void LimitSizeTo()
        {
            var subject = new Subject<IEnumerable<Person>>();
            var scheduler = new TestScheduler();
            var results = subject.ToObservableChangeSet(limitSizeTo: 100, scheduler: scheduler).AsAggregator();

            var people = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();

            subject.OnNext(people);

            scheduler.AdvanceBy(1);

            results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(100, "Should be 100 removes");
            results.Data.Count.Should().Be(100, "Should be 1 item in the cache");

            var expected = people.Skip(100).ToArray().OrderBy(p => p.Name).ToArray();
            var actual = results.Data.Items.OrderBy(p => p.Name).ToArray();
            actual.ShouldAllBeEquivalentTo(actual, "Only second hundred should be in the cache");
        }

        [Fact]
        public void ExpireAfterTime()
        {
            var subject = new Subject<IEnumerable<Person>>();
            var scheduler = new TestScheduler();
            var results = subject.ToObservableChangeSet<Person>(t => TimeSpan.FromMinutes(1), scheduler: scheduler).AsAggregator();

            var people = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();

            subject.OnNext(people);

            scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
            //scheduler.Start();
            results.Messages.Count.Should().Be(2, "Should be 300 messages");
            results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(200, "Should be 100 removes");
            results.Data.Count.Should().Be(0, "Should be no data in the cache");
        }
    }
}
