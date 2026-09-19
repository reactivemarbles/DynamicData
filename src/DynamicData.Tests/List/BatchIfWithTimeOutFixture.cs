using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class BatchIfWithTimeOutFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.ISignal<bool> _pausingSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();

    private readonly ChangeSetAggregator<Person> _results;

    private readonly TestScheduler _scheduler;

    private readonly ISourceList<Person> _source;

    public BatchIfWithTimeOutFixture()
    {
        _pausingSubject = new ReactiveUI.Primitives.Signals.Signal<bool>();
        _scheduler = new TestScheduler();
        _source = new SourceList<Person>();
        _results = _source.Connect().BufferIf(_pausingSubject, TimeSpan.FromMinutes(1), _scheduler).AsAggregator();
    }

    [Test]
    public async Task CanToggleSuspendResume()
    {
        _pausingSubject.OnNext(true);
        ////advance otherwise nothing happens
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.Add(new Person("A", 1));

        //go forward an arbitary amount of time
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("There should be no messages");

        _pausingSubject.OnNext(false);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.Add(new Person("B", 1));

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("There should be no messages");
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
        _pausingSubject.OnCompleted();
        _pausingSubject?.Dispose();
    }

    [Test]
    public async Task NoResultsWillBeReceivedIfPaused()
    {
        _pausingSubject.OnNext(true);
        //advance otherwise nothing happens
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);

        _source.Add(new Person("A", 1));

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

    [Test]
    public async Task WillApplyTimeout()
    {
        _pausingSubject.OnNext(true);

        //should timeout
        _scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);

        _source.Add(new Person("A", 1));

        //go forward an arbitary amount of time
        // _scheduler.AdvanceBy(TimeSpan.FromMinutes(1).Ticks);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("There should be no messages");
    }
}
