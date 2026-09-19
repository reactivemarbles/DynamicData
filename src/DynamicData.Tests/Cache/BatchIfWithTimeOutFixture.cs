using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class BatchIfTimedResumeFixture : IDisposable
{
    private readonly TestScheduler _scheduler;

    private readonly ISourceCache<Person, string> _source;

    public BatchIfTimedResumeFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceCache<Person, string>(p => p.Key);
    }

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task InitialPause()
    {
        var pausingSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();
        using var results = _source.Connect().BatchIf(pausingSubject, true, _scheduler).AsAggregator();
        // no results because the initial pause state is pause
        _source.AddOrUpdate(new Person("A", 1));
        await Assert.That(results.Data.Count).IsEqualTo(0);

        //resume and expect a result
        pausingSubject.OnNext(false);
        await Assert.That(results.Data.Count).IsEqualTo(1);

        //add another in the window where there is no pause
        _source.AddOrUpdate(new Person("B", 1));
        await Assert.That(results.Data.Count).IsEqualTo(2);

        // pause again
        pausingSubject.OnNext(true);
        _source.AddOrUpdate(new Person("C", 1));
        await Assert.That(results.Data.Count).IsEqualTo(2);

        //resume for the second time
        pausingSubject.OnNext(false);
        await Assert.That(results.Data.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Timeout()
    {
        var pausingSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();
        using var results = _source.Connect().BatchIf(pausingSubject, TimeSpan.FromSeconds(1), _scheduler).AsAggregator();
        // no results because the initial pause state is pause
        _source.AddOrUpdate(new Person("A", 1));
        await Assert.That(results.Data.Count).IsEqualTo(1);

        // pause and add
        pausingSubject.OnNext(true);
        _source.AddOrUpdate(new Person("B", 1));
        await Assert.That(results.Data.Count).IsEqualTo(1);

        //resume before timeout ends
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
        await Assert.That(results.Data.Count).IsEqualTo(1);

        pausingSubject.OnNext(false);
        await Assert.That(results.Data.Count).IsEqualTo(2);

        //pause and advance past timeout window
        pausingSubject.OnNext(true);
        _source.AddOrUpdate(new Person("C", 1));
        _scheduler.AdvanceBy(TimeSpan.FromSeconds(2.1).Ticks);
        await Assert.That(results.Data.Count).IsEqualTo(3);

        _source.AddOrUpdate(new Person("D", 1));
        await Assert.That(results.Data.Count).IsEqualTo(4);
    }
}

public class BatchIfWithTimeOutFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.ISignal<bool> _pausingSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();

    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly TestScheduler _scheduler;

    private readonly ISourceCache<Person, string> _source;

    public BatchIfWithTimeOutFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = _source.Connect().BatchIf(_pausingSubject, TimeSpan.FromMinutes(1), _scheduler).AsAggregator();
    }

    [Test]
    public async Task CanToggleSuspendResume()
    {
        _pausingSubject.OnNext(true);
        ////advance otherwise nothing happens
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.AddOrUpdate(new Person("A", 1));

        //go forward an arbitary amount of time
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("There should be no messages");

        _pausingSubject.OnNext(false);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.AddOrUpdate(new Person("B", 1));

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("There should be 2 messages");
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
        _pausingSubject.OnCompleted();
        _pausingSubject.Dispose();
    }

    [Test]
    public async Task NoResultsWillBeReceivedIfPaused()
    {
        _pausingSubject.OnNext(true);
        //advance otherwise nothing happens
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.AddOrUpdate(new Person("A", 1));

        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("There should be no messages");
    }

    [Test]
    public async Task PublishesOnIntervalEvent()
    {
        var intervalTimer = Observable.Interval(TimeSpan.FromMilliseconds(5), _scheduler).Select(_ => Unit.Default);
        var results = _source.Connect().BatchIf(_pausingSubject, true, intervalTimer, _scheduler).AsAggregator();

        //Buffering
        _source.AddOrUpdate(new Person("A", 1));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("There should be 0 messages");

        //Interval Fires and drains buffer
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("There should be 1 messages");

        //Buffering again
        _source.AddOrUpdate(new Person("B", 2));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("There should be 1 messages");

        //Interval Fires and drains buffer
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("There should be 2 messages");

        //Buffering again
        _source.AddOrUpdate(new Person("C", 3));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("There should be 2 messages");

        //Interval Fires and drains buffer
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(3).Because("There should be 3 messages");
    }

    [Test]
    public async Task PublishesOnTimerCompletion()
    {
        var intervalTimer = Observable.Timer(TimeSpan.FromMilliseconds(5), _scheduler).Select(_ => Unit.Default);
        var results = _source.Connect().BatchIf(_pausingSubject, true, intervalTimer, _scheduler).AsAggregator();

        //Buffering
        _source.AddOrUpdate(new Person("A", 1));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(0).Because("There should be 0 messages");

        //Timer should event, buffered items delivered
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(5).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("There should be 1 messages");

        //Unbuffered from here
        _source.AddOrUpdate(new Person("B", 2));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("There should be 2 messages");

        //Unbuffered from here
        _source.AddOrUpdate(new Person("C", 3));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
        await Assert.That(results.Messages.Count).IsEqualTo(3).Because("There should be 3 messages");
    }

    [Test]
    public async Task ResultsWillBeReceivedIfNotPaused()
    {
        _source.AddOrUpdate(new Person("A", 1));

        //go forward an arbitary amount of time
        _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 update");
    }

    [Test]
    public async Task WillApplyTimeout()
    {
        _pausingSubject.OnNext(true);

        //should timeout
        _scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);

        _source.AddOrUpdate(new Person("A", 1));

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("There should be 1 messages");
    }
}
