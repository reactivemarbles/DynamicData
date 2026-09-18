using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class BatchIfFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.ISignal<bool> _pausingSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();

    private readonly ChangeSetAggregator<Person> _results;

    private readonly TestScheduler _scheduler;

    private readonly ISourceList<Person> _source;

    public BatchIfFixture()
    {
        _pausingSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();
        _scheduler = new TestScheduler();
        _source = new SourceList<Person>();
        _results = _source.Connect().BufferIf(_pausingSubject, _scheduler).AsAggregator();
    }

    [Test]
    public async Task CanToggleSuspendResume()
    {
        _pausingSubject.OnNext(true);
        ////advance otherwise nothing happens
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.Add(new Person("A", 1));

        //go forward an arbitary amount of time
        _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("There should be no messages");

        _pausingSubject.OnNext(false);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
        _source.Add(new Person("B", 1));

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("There should be no messages");
    }

    /// <summary>
    /// Test case to prove the issue and fix to DynamicData GitHub issue #275 - BufferIf race condition
    /// </summary>
    [Test]
    public async Task ChangesNotLostIfConsumerIsRunningOnDifferentThread()
    {
        var producerScheduler = new TestScheduler();
        var consumerScheduler = new TestScheduler();

        //Note consumer is running on a different scheduler
        _source.Connect().BufferIf(_pausingSubject, producerScheduler).ObserveOn(consumerScheduler).Bind(out var target).AsAggregator();

        _source.Add(new Person("A", 1));

        producerScheduler.AdvanceBy(1);
        consumerScheduler.AdvanceBy(1);

        await Assert.That(target.Count).IsEqualTo(1).Because("There should be 1 message");

        _pausingSubject.OnNext(true);

        producerScheduler.AdvanceBy(1);
        consumerScheduler.AdvanceBy(1);

        _source.Add(new Person("B", 2));

        producerScheduler.AdvanceBy(1);
        consumerScheduler.AdvanceBy(1);

        await Assert.That(target.Count).IsEqualTo(1).Because("There should be 1 message");

        _pausingSubject.OnNext(false);

        producerScheduler.AdvanceBy(1);

        //Target doesnt get the messages until its scheduler runs, but the
        //messages shouldnt be lost
        await Assert.That(target.Count).IsEqualTo(1).Because("There should be 1 message");

        consumerScheduler.AdvanceBy(1);

        await Assert.That(target.Count).IsEqualTo(2).Because("There should be 2 message");
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
        _pausingSubject.Dispose();
    }

    [Test]
    public async Task NoResultsWillBeReceivedIfPaused()
    {
        _pausingSubject.OnNext(true);
        //advance otherwise nothing happens
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.Add(new Person("A", 1));

        //go forward an arbitary amount of time
        _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("There should be no messages");
    }

    [Test]
    public async Task ResultsWillBeReceivedIfNotPaused()
    {
        _source.Add(new Person("A", 1));

        //go forward an arbitary amount of time
        _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 update");
    }
}
