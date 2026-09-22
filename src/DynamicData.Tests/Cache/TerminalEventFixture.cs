using System;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Terminal-event behaviour for operators that subscribe internally without exposing the
/// subscription. A missing error handler on one of those internal subscriptions does not fail
/// loudly: the exception is rethrown wherever delivery happened to be, and the subscriber is left
/// believing the sequence ended normally.
/// </summary>
public class TerminalEventFixture
{
    [Fact]
    public void WatchFailsWhenTheSourceFails()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        using var cache = new IntermediateCache<Person, string>(source);

        var actualError = default(Exception);
        var isCompleted = false;
        using var subscription = cache.Watch("Name1").Subscribe(static _ => { }, error => actualError = error, () => isCompleted = true);

        var expectedError = new Exception("Test Exception");
        source.OnError(expectedError);

        actualError.Should().BeSameAs(expectedError, "a watcher should be told when the source fails");
        isCompleted.Should().BeFalse("the source failed, it did not complete");
    }

    [Fact]
    public void WatchCompletesWhenTheSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        using var cache = new IntermediateCache<Person, string>(source);

        var actualError = default(Exception);
        var isCompleted = false;
        using var subscription = cache.Watch("Name1").Subscribe(static _ => { }, error => actualError = error, () => isCompleted = true);

        source.OnCompleted();

        isCompleted.Should().BeTrue("the source completed");
        actualError.Should().BeNull("no error occurred");
    }

    [Fact]
    public void LimitSizeToCompletesWhenTheSourceIsDisposed()
    {
        // The eviction stream is driven by source.Connect(). A plain SourceCache has no upstream that
        // can fail, so only the completion path is reachable here, but the handler that carries it is
        // the same one that now carries a failure for any ISourceCache implementation that does fail.
        // Completion also travels the scheduler now rather than firing ahead of whatever it has queued.
        var source = new SourceCache<Person, string>(p => p.Key);

        var actualError = default(Exception);
        var isCompleted = false;
        using var subscription = source.LimitSizeTo(10, Scheduler.Immediate).Subscribe(static _ => { }, error => actualError = error, () => isCompleted = true);

        source.Dispose();

        isCompleted.Should().BeTrue("the source completed");
        actualError.Should().BeNull("no error occurred");
    }
}
