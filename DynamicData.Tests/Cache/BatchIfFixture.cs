using System;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Cache
{
    
    public class BatchIfFixture: IDisposable
    {
        private readonly ISourceCache<Person, string> _source;
        private readonly ChangeSetAggregator<Person, string> _results;
        private readonly TestScheduler _scheduler;

        private readonly ISubject<bool> _pausingSubject = new Subject<bool>();

        public  BatchIfFixture()
        {
            _pausingSubject = new Subject<bool>();
            _scheduler = new TestScheduler();
            _source = new SourceCache<Person, string>(p => p.Key);
            _results = _source.Connect().BatchIf(_pausingSubject, _scheduler).AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Fact]
        public void NoResultsWillBeReceivedIfPaused()
        {
            _pausingSubject.OnNext(true);
            //advance otherwise nothing happens
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

            _source.AddOrUpdate(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
            _results.Messages.Count.Should().Be(0, "There should be no messages");
        }

        [Fact]
        public void ResultsWillBeReceivedIfNotPaused()
        {
            _source.AddOrUpdate(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
            _results.Messages.Count.Should().Be(1, "Should be 1 update");
        }

        [Fact]
        public void CanToggleSuspendResume()
        {
            _pausingSubject.OnNext(true);
            ////advance otherwise nothing happens
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

            _source.AddOrUpdate(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
            _results.Messages.Count.Should().Be(0, "There should be no messages");

            _pausingSubject.OnNext(false);
            _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

            _source.AddOrUpdate(new Person("B", 1));

            _results.Messages.Count.Should().Be(2, "There should be no messages");
        }
    }
}
