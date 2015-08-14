using System;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class BatchIfWithTimeOutFixture
    {
        private ISourceList<Person> _source;
        private ChangeSetAggregator<Person> _results;
        private TestScheduler _scheduler;

        private ISubject<bool> _pausingSubject = new Subject<bool>();


        [SetUp]
        public void MyTestInitialize()
        {
            _pausingSubject = new Subject<bool>();
            _scheduler = new TestScheduler();
            _source = new SourceList<Person>();
            _results = _source.Connect().BufferIf(_pausingSubject,TimeSpan.FromMinutes(1), _scheduler).AsAggregator();

        }

        [TearDown]
        public void Cleanup()
        {
            _results.Dispose();
            _source.Dispose();
            _pausingSubject.OnCompleted();
        }


        [Test]
        public void WillApplyTimeout()
        {
            _pausingSubject.OnNext(true);

            //should timeout 
            _scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);

			_source.Add(new Person("A", 1));

			//go forward an arbitary amount of time
			// _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
			Assert.AreEqual(1, _results.Messages.Count, "There should be no messages");
        }

        [Test]
        public void NoResultsWillBeReceivedIfPaused()
        {
            _pausingSubject.OnNext(true);
            //advance otherwise nothing happens
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);


            _source.Add(new Person("A", 1));

            Assert.AreEqual(0, _results.Messages.Count, "There should be no messages");
        }

        [Test]
        public void ResultsWillBeReceivedIfNotPaused()
        {
            _source.Add(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 update");
        }

        [Test]
        public void CanToggleSuspendResume()
        {
            _pausingSubject.OnNext(true);
            ////advance otherwise nothing happens
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

            _source.Add(new Person("A", 1));

            //go forward an arbitary amount of time
            Assert.AreEqual(0, _results.Messages.Count, "There should be no messages");

            _pausingSubject.OnNext(false);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

            _source.Add(new Person("B", 1));

            Assert.AreEqual(2, _results.Messages.Count, "There should be no messages");
        }
    }
}