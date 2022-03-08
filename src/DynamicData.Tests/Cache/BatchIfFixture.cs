using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.Cache;

public class BatchIfFixture : IDisposable
{
    private readonly ISubject<bool> _pausingSubject = new Subject<bool>();

    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly TestScheduler _scheduler;

    private readonly ISourceCache<Person, string> _source;

    public BatchIfFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = _source.Connect().BatchIf(_pausingSubject, _scheduler).AsAggregator();

        // _results = _source.Connect().BatchIf(new BehaviorSubject<bool>(true), scheduler: _scheduler).AsAggregator();
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

        _results.Messages.Count.Should().Be(2, "There should be 2 messages");

        _pausingSubject.OnNext(true);
        ////advance otherwise nothing happens
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.AddOrUpdate(new Person("C", 1));

        //go forward an arbitary amount of time
        _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
        _results.Messages.Count.Should().Be(2, "There should be 2 messages");

        _pausingSubject.OnNext(false);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _results.Messages.Count.Should().Be(3, "There should be 3 messages");
    }

    /// <summary>
    /// Test case to prove the issue and fix to DynamicData GitHub issue #98 - BatchIf race condition
    /// </summary>
    [Fact]
    public void ChangesNotLostIfConsumerIsRunningOnDifferentThread()
    {
        var producerScheduler = new TestScheduler();
        var consumerScheduler = new TestScheduler();

        //Note consumer is running on a different scheduler
        _source.Connect().BatchIf(_pausingSubject, producerScheduler).ObserveOn(consumerScheduler).Bind(out var target).AsAggregator();

        _source.AddOrUpdate(new Person("A", 1));

        producerScheduler.AdvanceBy(1);
        consumerScheduler.AdvanceBy(1);

        target.Count.Should().Be(1, "There should be 1 message");

        _pausingSubject.OnNext(true);

        producerScheduler.AdvanceBy(1);
        consumerScheduler.AdvanceBy(1);

        _source.AddOrUpdate(new Person("B", 2));

        producerScheduler.AdvanceBy(1);
        consumerScheduler.AdvanceBy(1);

        target.Count.Should().Be(1, "There should be 1 message");

        _pausingSubject.OnNext(false);

        producerScheduler.AdvanceBy(1);

        //Target doesnt get the messages until its scheduler runs, but the
        //messages shouldnt be lost
        target.Count.Should().Be(1, "There should be 1 message");

        consumerScheduler.AdvanceBy(1);

        target.Count.Should().Be(2, "There should be 2 message");
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
}
