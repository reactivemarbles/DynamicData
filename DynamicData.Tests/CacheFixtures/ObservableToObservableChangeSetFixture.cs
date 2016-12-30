using System;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class ObservableToObservableChangeSetFixture
    {
        [Test]
        public void OnNextFiresAdd()
        {
            var subject = new Subject<Person>();

            var results = subject.ToObservableChangeSet(p => p.Key).AsAggregator();
            var person = new Person("A", 1);
            subject.OnNext(person);

            Assert.AreEqual(1, results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void OnNextForAmendedItemFiresUpdate()
        {
            var subject = new Subject<Person>();

            var results = subject.ToObservableChangeSet(p => p.Key).AsAggregator();
            var person = new Person("A", 1);
            subject.OnNext(person);

            var personamend = new Person("A", 2);
            subject.OnNext(personamend);

            Assert.AreEqual(2, results.Messages.Count, "Should be 2 message");
            Assert.AreEqual(1, results.Messages[1].Updates, "Should be 1 updates");
            Assert.AreEqual(1, results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(personamend, results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void OnNextProducesAndAddChangeForSingleItem()
        {
            var subject = new Subject<Person>();

            var results = subject.ToObservableChangeSet(p => p.Key).AsAggregator();
            var person = new Person("A", 1);
            subject.OnNext(person);

            Assert.AreEqual(1, results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, results.Data.Items.First(), "Should be same person");
        }

        [Test]
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

            Assert.AreEqual(200, results.Messages.Sum(x => x.Adds), "Should be 200 adds");
            Assert.AreEqual(100, results.Messages.Sum(x => x.Removes), "Should be 100 removes");
            Assert.AreEqual(100, results.Data.Count);

            var expected = items.Skip(100).ToArray().OrderBy(p => p.Name).ToArray();
            var actual = results.Data.Items.OrderBy(p => p.Name).ToArray();
            CollectionAssert.AreEqual(expected, actual, "Only second hundred should be in the cache");
        }

        [Test]
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

            Assert.AreEqual(201, results.Messages.Count, "Should be 300 messages");
            Assert.AreEqual(200, results.Messages.Sum(x => x.Adds), "Should be 200 adds");
            Assert.AreEqual(200, results.Messages.Sum(x => x.Removes), "Should be 100 removes");
            Assert.AreEqual(0, results.Data.Count, "Should be no data in the cache");
        }
    }
}
