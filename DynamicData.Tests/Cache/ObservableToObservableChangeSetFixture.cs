using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Cache
{
    public class QuickAndDirtyPerformanceMeasure
    {
        private static readonly Person[] _people = Enumerable.Range(1, 56_000).Select(i => new Person($"Name {i}", i)).ToArray();
        private readonly SourceCache<Person, string> _peopleCache = new SourceCache<Person, string>(p=> p.Name);

        [Fact]
        public void AddLotsOfItems()
        {
            _peopleCache.AddOrUpdate(_people);
        }

        [Fact]
        public void DoSomeStuffWithAnExtraOrdinarilySimplisticMeansOfMeasuringPerformance()
        {
            var mySubscriptions = _peopleCache
                .Connect()
                .Do(_ => { })
                .Transform(x => x) //
                .Do(_ => { })
                .Subscribe();

            _peopleCache.AddOrUpdate(_people);
        }
    }



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

            items.ForEach(subject.OnNext);

            scheduler.Start();

            results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(100, "Should be 100 removes");
            results.Data.Count.Should().Be(100);

            var expected = items.Skip(100).ToArray().OrderBy(p => p.Name).ToArray();
            var actual = results.Data.Items.OrderBy(p => p.Name).ToArray();
            expected.ShouldAllBeEquivalentTo(actual, "Only second hundred should be in the cache");
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

            results.Messages.Count.Should().Be(201, "Should be 201 messages");
            results.Messages.Sum(x => x.Adds).Should().Be(200, "Should be 200 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(200, "Should be 200 removes");
            results.Data.Count.Should().Be(0, "Should be no data in the cache");
        }

        [Fact]
        public void ExpireAfterTimeWithKey()
        {
            var subject = new Subject<Person>();
            var scheduler = new TestScheduler();
            var results = subject.ToObservableChangeSet(p => p.Key, expireAfter: t => TimeSpan.FromMinutes(1), scheduler: scheduler).AsAggregator();

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
            var source =
                Observable.Interval(TimeSpan.FromSeconds(1), scheduler: scheduler)
                    .Take(30)
                    .Select(i => (int)i)
                    .Select(i => new Person("p" + i.ToString("000"), i));

            var results = source.ToObservableChangeSet(expireAfter: t => TimeSpan.FromSeconds(10), scheduler: scheduler).AsAggregator();

            scheduler.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);

            Console.WriteLine(results.Messages.Count);
            Console.WriteLine(results.Messages.Sum(x => x.Adds));
            Console.WriteLine(results.Messages.Sum(x => x.Removes));

            results.Messages.Count.Should().Be(50, "Should be 50 messages");
            results.Messages.Sum(x => x.Adds).Should().Be(30, "Should be 30 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(20, "Should be 20 removes");
            results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
        }

        [Fact]
        public void ExpireAfterTimeDynamicWithKey()
        {
            var scheduler = new TestScheduler();
            var source =
                Observable.Interval(TimeSpan.FromSeconds(1), scheduler: scheduler)
                    .Take(30)
                    .Select(i => (int)i)
                    .Select(i => new Person("p" + i.ToString("000"), i));

            var results = source.ToObservableChangeSet(p => p.Key, expireAfter: t => TimeSpan.FromSeconds(10), scheduler: scheduler).AsAggregator();

            scheduler.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);

            Console.WriteLine(results.Messages.Count);
            Console.WriteLine(results.Messages.Sum(x => x.Adds));
            Console.WriteLine(results.Messages.Sum(x => x.Removes));

            results.Messages.Count.Should().Be(50, "Should be 50 messages");
            results.Messages.Sum(x => x.Adds).Should().Be(30, "Should be 30 adds");
            results.Messages.Sum(x => x.Removes).Should().Be(20, "Should be 20 removes");
            results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
        }
    }
}