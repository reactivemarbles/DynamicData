using System;
using System.Linq;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class SizeLimitFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;
        private TestScheduler _scheduler;
        private IDisposable _sizeLimiter;
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

        [SetUp]
        public void Initialise()
        {
            _scheduler = new TestScheduler();
            _source = new SourceList<Person>();
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
            _source.Add(person);

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AddMoreThanLimit()
        {
            var people = _generator.Take(100).OrderBy(p => p.Name).ToArray();
            _source.AddRange(people);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

            _source.Dispose();
            Assert.AreEqual(10, _results.Data.Count, "Should be 10 items in the cache");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");
            Assert.AreEqual(100, _results.Messages[0].Adds, "Should be 100 adds in the first update");
            Assert.AreEqual(90, _results.Messages[1].Removes, "Should be 90 removes in the second update");
        }

        [Test]
        public void AddMoreThanLimitInBatched()
        {
            _source.AddRange(_generator.Take(10).ToArray());
            _source.AddRange(_generator.Take(10).ToArray());

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
            Assert.AreEqual(10, _results.Data.Count, "Should be 10 items in the cache");
            Assert.AreEqual(3, _results.Messages.Count, "Should be 3 updates");
            Assert.AreEqual(10, _results.Messages[0].Adds, "Should be 10 adds in the first update");
            Assert.AreEqual(10, _results.Messages[1].Adds, "Should be 10 adds in the second update");
            Assert.AreEqual(10, _results.Messages[2].Removes, "Should be 10 removes in the third update");
        }

        [Test]
        public void Add()
        {
            var person = _generator.Take(1).First();
            _source.Add(person);

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(person, _results.Data.Items.First(), "Should be same person");
        }

        [Test]

        public void ForceError()
        {
            var person = _generator.Take(1).First();
            Assert.Throws<ArgumentOutOfRangeException>(() => _source.RemoveAt(1));
        }

        [Test]
        public void HandleError()
        {
            Exception exception = null;
            _source.Edit(innerList => innerList.RemoveAt(1), ex => exception = ex);
            Assert.IsNotNull(exception);
        }

        [Test]
        public void ThrowsIfSizeLimitIsZero()
        {
            // Initialise();
            Assert.Throws<ArgumentException>(() => new SourceCache<Person, string>(p => p.Key).LimitSizeTo(0));
        }
    }
}
