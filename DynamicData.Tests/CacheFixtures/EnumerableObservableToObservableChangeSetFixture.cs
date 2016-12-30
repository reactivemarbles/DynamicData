using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class EnumerableObservableToObservableChangeSetFixture
    {
        [Test]
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

            Assert.AreEqual(1, results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(3, results.Data.Count, "Should be 1 item in the cache");
            CollectionAssert.AreEquivalent(people, results.Data.Items, "Lists should be equivalent");
        }

        [Test]
        public void LimitSizeTo()
        {
            var subject = new Subject<IEnumerable<Person>>();
            var scheduler = new TestScheduler();
            var results = subject.ToObservableChangeSet(limitSizeTo: 100, scheduler: scheduler).AsAggregator();

            var people = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();

            subject.OnNext(people);

            scheduler.AdvanceBy(1);

          // Assert.AreEqual(2, results.Messages.Count, "Should be 300 messages");
            Assert.AreEqual(200, results.Messages.Sum(x => x.Adds), "Should be 200 adds");
            Assert.AreEqual(100, results.Messages.Sum(x => x.Removes), "Should be 100 removes");
            Assert.AreEqual(100, results.Data.Count, "Should be 1 item in the cache");

            var expected = people.Skip(100).ToArray().OrderBy(p => p.Name).ToArray();
            var actual = results.Data.Items.OrderBy(p => p.Name).ToArray();
            CollectionAssert.AreEqual(expected, actual, "Only second hundred should be in the cache");
        }

        [Test]
        public void ExpireAfterTime()
        {
            var subject = new Subject<IEnumerable<Person>>();
            var scheduler = new TestScheduler();
            var results = subject.ToObservableChangeSet<Person>(t => TimeSpan.FromMinutes(1), scheduler: scheduler).AsAggregator();

            var people = Enumerable.Range(1, 200).Select(i => new Person("p" + i.ToString("000"), i)).ToArray();

            subject.OnNext(people);

            scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
            //scheduler.Start();
            Assert.AreEqual(2, results.Messages.Count, "Should be 300 messages");
            Assert.AreEqual(200, results.Messages.Sum(x => x.Adds), "Should be 200 adds");
            Assert.AreEqual(200, results.Messages.Sum(x => x.Removes), "Should be 100 removes");
            Assert.AreEqual(0, results.Data.Count, "Should be no data in the cache");
        }
    }
}
