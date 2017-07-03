using System;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using NUnit.Framework;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class BatchIfFixture
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
            _results = _source.Connect().BufferIf(_pausingSubject, _scheduler).AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Test]
        public void NoResultsWillBeReceivedIfPaused()
        {
            _pausingSubject.OnNext(true);
            //advance otherwise nothing happens
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

            _source.Add(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
            _results.Messages.Count.Should().Be(0, "There should be no messages");
        }

        [Test]
        public void ResultsWillBeReceivedIfNotPaused()
        {
            _source.Add(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
            _results.Messages.Count.Should().Be(1, "Should be 1 update");
        }

        [Test]
        public void CanToggleSuspendResume()
        {
            _pausingSubject.OnNext(true);
            ////advance otherwise nothing happens
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

            _source.Add(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
            _results.Messages.Count.Should().Be(0, "There should be no messages");

            _pausingSubject.OnNext(false);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
            _source.Add(new Person("B", 1));

            _results.Messages.Count.Should().Be(2, "There should be no messages");
        }
    }
}
