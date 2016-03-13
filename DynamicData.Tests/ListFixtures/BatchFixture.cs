using System;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;
using System.Reactive.Linq;
using System.Reactive.Subjects;


namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class BatchFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;
        private TestScheduler _scheduler;


        [SetUp]
        public void SetUp()
        {
            _scheduler = new TestScheduler();
            _source = new SourceList<Person>();
            _results = _source.Connect()
							.Buffer(TimeSpan.FromMinutes(1), _scheduler)
							.FlattenBufferResult()
							.AsAggregator();

        }

        [TearDown]
        public void Cleanup()
        {
            _results.Dispose();
            _source.Dispose();
        }


        [Test]
        public void NoResultsWillBeReceivedBeforeClosingBuffer()
        {

            _source.Add(new Person("A", 1));
            Assert.AreEqual(0, _results.Messages.Count, "There should be no messages");
        }

        [Test]
        public void ResultsWillBeReceivedAfterClosingBuffer()
        {
            _source.Add(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 update");
        }


    }
}