using System;
using System.Linq;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
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

        public void Dispose()
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

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            _results.Data.Items.First().Should().Be(person, "Should be same person");
        }

        [Test]
        public void AddMoreThanLimit()
        {
            var people = _generator.Take(100).OrderBy(p => p.Name).ToArray();
            _source.AddRange(people);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

            _source.Dispose();
            _results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
            _results.Messages.Count.Should().Be(2, "Should be 2 updates");
            _results.Messages[0].Adds.Should().Be(100, "Should be 100 adds in the first update");
            _results.Messages[1].Removes.Should().Be(90, "Should be 90 removes in the second update");
        }

        [Test]
        public void AddMoreThanLimitInBatched()
        {
            _source.AddRange(_generator.Take(10).ToArray());
            _source.AddRange(_generator.Take(10).ToArray());

            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
            _results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
            _results.Messages.Count.Should().Be(3, "Should be 3 updates");
            _results.Messages[0].Adds.Should().Be(10, "Should be 10 adds in the first update");
            _results.Messages[1].Adds.Should().Be(10, "Should be 10 adds in the second update");
            _results.Messages[2].Removes.Should().Be(10, "Should be 10 removes in the third update");
        }

        [Test]
        public void Add()
        {
            var person = _generator.Take(1).First();
            _source.Add(person);

            _results.Messages.Count.Should().Be(1, "Should be 1 updates");
            _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
            _results.Data.Items.First().Should().Be(person, "Should be same person");
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
            exception.Should().NotBeNull();
        }

        [Test]
        public void ThrowsIfSizeLimitIsZero()
        {
            // Initialise();
            Assert.Throws<ArgumentException>(() => new SourceCache<Person, string>(p => p.Key).LimitSizeTo(0));
        }
    }
}
