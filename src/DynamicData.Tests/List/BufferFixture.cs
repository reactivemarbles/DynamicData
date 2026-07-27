using System;
using System.Reactive.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using DynamicData.Binding;

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

    [Fact]
    public void BufferIfCompletesWhenTheSourceCompletes()
    {
        var completed = false;

        using var source = new Subject<IChangeSet<Person>>();
        using var subscription = source.BufferIf(Observable.Return(false), Scheduler.Immediate).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void BufferIfFlushesHeldChangesBeforeCompleting()
    {
        var received = 0;
        var completed = false;

        using var source = new Subject<IChangeSet<Person>>();
        using var subscription = source.BufferIf(Observable.Return(true), Scheduler.Immediate).Subscribe(_ => received++, () => completed = true);

        source.OnNext(new ChangeSet<Person> { new Change<Person>(ListChangeReason.Add, new Person("a", 1), 0) });
        source.OnCompleted();

        received.Should().Be(1, "changes held back by the pause would otherwise be lost");
        completed.Should().BeTrue();
    }
}
