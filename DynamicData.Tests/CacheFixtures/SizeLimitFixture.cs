using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    internal class SizeLimitFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<Person, string> _results;
        private TestScheduler _scheduler;
        private IDisposable _sizeLimiter;

        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        [SetUp]
        public void Initialise()
        {
            _scheduler = new TestScheduler();
            _source = new SourceCache<Person, string>(p => p.Key);
            _sizeLimiter = _source.LimitSizeTo(10, _scheduler).Subscribe();
            _results = _source.Connect().AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _sizeLimiter.Dispose();
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void AddLessThanLimit()
        {
            var person = _generator.Take(1).First();
            _source.AddOrUpdate(person);

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AddMoreThanLimit()
        {
            var people = _generator.Take(100).OrderBy(p => p.Name).ToArray();
            _source.AddOrUpdate(people);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

            _source.Dispose();
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(100, _results.Messages[0].Adds, "Should be 100 adds in the first update");
            Assert.AreEqual(90, _results.Messages[1].Removes, "Should be 90 removes in the second update");
        }

        [Test]
        public void AddMoreThanLimitInBatched()
        {
            _source.AddOrUpdate(_generator.Take(10).ToArray());
            _source.AddOrUpdate(_generator.Take(10).ToArray());

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

            Assert.AreEqual(3, _results.Messages.Count, "Should be 3 updates");
            Assert.AreEqual(10, _results.Messages[0].Adds, "Should be 10 adds in the first update");
            Assert.AreEqual(10, _results.Messages[1].Adds, "Should be 10 adds in the second update");
            Assert.AreEqual(10, _results.Messages[2].Removes, "Should be 10 removes in the third update");
        }

        [Test]
        public void Add()
        {
            var person = _generator.Take(1).First();
            _source.AddOrUpdate(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void ThrowsIfSizeLimitIsZero()
        {
            // Initialise();
            Assert.Throws<ArgumentException>(() => new SourceCache<Person, string>(p => p.Key).LimitSizeTo(0));
            ;
        }

        [Test]
        public void OnCompleteIsInvokedWhenSourceIsDisposed()
        {
            bool completed = false;

            var subscriber = _source.LimitSizeTo(10)
                                    .Finally(() => completed = true)
                                    .Subscribe(updates => { Console.WriteLine(); });

            _source.Dispose();

            Assert.IsTrue(completed, "Completed has not been called");
        }
    }
}
