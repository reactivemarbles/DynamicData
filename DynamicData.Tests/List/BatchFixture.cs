using System;
using DynamicData.Tests.Domain;
using Microsoft.Reactive.Testing;
using Xunit;
using System.Reactive.Linq;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class BatchFixture: IDisposable
    {
        private readonly ISourceList<Person> _source;
        private readonly ChangeSetAggregator<Person> _results;
        private readonly TestScheduler _scheduler;

        public  BatchFixture()
        {
            _scheduler = new TestScheduler();
            _source = new SourceList<Person>();
            _results = _source.Connect()
                              .Buffer(TimeSpan.FromMinutes(1), _scheduler)
                              .FlattenBufferResult()
                              .AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Fact]
        public void NoResultsWillBeReceivedBeforeClosingBuffer()
        {
            _source.Add(new Person("A", 1));
            _results.Messages.Count.Should().Be(0, "There should be no messages");
        }

        [Fact]
        public void ResultsWillBeReceivedAfterClosingBuffer()
        {
            _source.Add(new Person("A", 1));

            //go forward an arbitary amount of time
            _scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
            _results.Messages.Count.Should().Be(1, "Should be 1 update");
        }
    }
}
