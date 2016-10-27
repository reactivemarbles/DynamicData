using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class ExpireAfterFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;

        private TestScheduler _scheduler;

        [SetUp]
        public void MyTestInitialize()
        {
            _scheduler = new TestScheduler();
            _source = new SourceList<Person>();
            _results = _source.Connect().AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Test]
        public void ComplexRemove()
        {
            Func<Person, TimeSpan?> removeFunc = t =>
            {
                if (t.Age <= 40)
                    return TimeSpan.FromSeconds(5);

                if (t.Age <= 80)
                    return TimeSpan.FromSeconds(7);
                return null;
            };

            const int size = 100;
            Person[] items = Enumerable.Range(1, size).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _source.AddRange(items);

            var remover = _source.ExpireAfter(removeFunc, _scheduler).Subscribe();
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5010).Ticks);

            Assert.AreEqual(60, _source.Count, "40 items should have been removed from the cache");

            _scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);
            Assert.AreEqual(20, _source.Count, "80 items should have been removed from the cache");

            remover.Dispose();
        }

        [Test]
        public void ItemAddedIsExpired()
        {
            var remover = _source.ExpireAfter(p => TimeSpan.FromMilliseconds(100), _scheduler).Subscribe();

            _source.Add(new Person("Name1", 10));

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(200).Ticks);
            remover.Dispose();

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 adds in the first update");
            Assert.AreEqual(1, _results.Messages[1].Removes, "Should be 1 removes in the second update");
        }

        [Test]
        public void ExpireIsCancelledWhenUpdated()
        {
            var remover = _source.ExpireAfter(p => TimeSpan.FromMilliseconds(100), _scheduler).Subscribe();

            var p1 = new Person("Name1", 20);
            var p2 = new Person("Name1", 21);

            _source.Add(p1);

            _source.Replace(p1, p2);

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(200).Ticks);
            remover.Dispose();
            Assert.AreEqual(0, _results.Data.Count, "Should be no data in the cache");
            Assert.AreEqual(3, _results.Messages.Count, "Should be 3 updates");
            Assert.AreEqual(1, _results.Messages[0].Adds, "Should be 1 add in the first message");
            Assert.AreEqual(1, _results.Messages[1].Replaced, "Should be 1 update in the second message");
            Assert.AreEqual(1, _results.Messages[2].Removes, "Should be 1 remove in the 3rd message");
        }

        [Test]
        public void CanHandleABatchOfUpdates()
        {
            var remover = _source.ExpireAfter(p => TimeSpan.FromMilliseconds(100), _scheduler).Subscribe();
            const int size = 100;
            Person[] items = Enumerable.Range(1, size).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            _source.AddRange(items);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(200).Ticks);
            remover.Dispose();

            Assert.AreEqual(0, _results.Data.Count, "Should be no data in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(100, _results.Messages[0].Adds, "Should be 100 adds in the first message");
            Assert.AreEqual(100, _results.Messages[1].Removes, "Should be 100 removes in the second message");
        }
    }
}
