using System;
using System.Reactive.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.List;

public class BufferFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person> _results;

    private readonly TestScheduler _scheduler;

    private readonly ISourceList<Person> _source;

    public BufferFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceList<Person>();
        _results = _source.Connect().Buffer(TimeSpan.FromMinutes(1), _scheduler).FlattenBufferResult().AsAggregator();
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
