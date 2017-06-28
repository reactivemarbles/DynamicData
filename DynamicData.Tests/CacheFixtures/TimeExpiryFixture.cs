using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture()]
    internal class TimeExpiryFixture
    {
        private ISourceCache<Person, string> _cache;
        private IDisposable _remover;
        private ChangeSetAggregator<Person, string> _results;

        private TestScheduler _scheduler;

        [SetUp]
        public void MyTestInitialize()
        {
            _scheduler = new TestScheduler();

            _cache = new SourceCache<Person, string>(p => p.Key);
            _results = new ChangeSetAggregator<Person, string>(_cache.Connect());
            _remover = _cache.ExpireAfter(p => TimeSpan.FromMilliseconds(100), _scheduler).Subscribe();
        }

        [TearDown]
        public void Cleanup()
        {
            //    _remover.Dispose();
            _results.Dispose();
            _remover.Dispose();
            _cache.Dispose();
        }

        [Test()]
        public void AutoRemove()
        {
            TimeSpan? RemoveFunc(Person t)
            {
                if (t.Age < 40)
                    return TimeSpan.FromSeconds(4);

                if (t.Age < 80)
                    return TimeSpan.FromSeconds(7);
                return null;
            }

            const int size = 100;
            Person[] items = Enumerable.Range(1, size).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();
            _cache.AddOrUpdate(items);

            var xxx = _cache.ExpireAfter(RemoveFunc, _scheduler).Subscribe();
            _scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);

            _scheduler.AdvanceBy(TimeSpan.FromSeconds(5).Ticks);

            xxx.Dispose();
        }

        [Test]
        public void ItemAddedIsExpired()
        {
            _cache.AddOrUpdate(new Person("Name1", 10));

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(1, "Should be 1 adds in the first update");
            _results.Messages[1].Removes.Should().Be(1, "Should be 1 removes in the second update");
        }

        [Test]
        public void ExpireIsCancelledWhenUpdated()
        {
            _cache.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Name1", 20));
                updater.AddOrUpdate(new Person("Name1", 21));
            });

            _scheduler.AdvanceBy(TimeSpan.FromSeconds(150).Ticks);

            _results.Data.Count.Should().Be(0, "Should be no data in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(1, "Should be 1 add in the first message");
            _results.Messages[0].Updates.Should().Be(1, "Should be 1 update in the first message");
            _results.Messages[1].Removes.Should().Be(1, "Should be 1 remove in the second message");
        }

        [Test]
        public void CanHandleABatchOfUpdates()
        {
            const int size = 100;
            Person[] items = Enumerable.Range(1, size).Select(i => new Person("Name.{0}".FormatWith(i), i)).ToArray();

            _cache.AddOrUpdate(items);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

            _results.Data.Count.Should().Be(0, "Should be no data in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(100, "Should be 100 adds in the first message");
            _results.Messages[1].Removes.Should().Be(100, "Should be 100 removes in the second message");
        }
    }
}
