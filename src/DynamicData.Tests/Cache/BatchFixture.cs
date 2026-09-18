using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class BatchFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly TestScheduler _scheduler;

    private readonly ISourceCache<Person, string> _source;

    public BatchFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = _source.Connect().Batch(TimeSpan.FromMinutes(1), _scheduler).AsAggregator();
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
    }

    [Test]
    public async Task NoResultsWillBeReceivedBeforeClosingBuffer()
    {
        _source.AddOrUpdate(new Person("A", 1));
        await Assert.That(_results.Messages.Count).IsEqualTo(0).Because("There should be no messages");
    }

    [Test]
    public async Task ResultsWillBeReceivedAfterClosingBuffer()
    {
        _source.AddOrUpdate(new Person("A", 1));

        //go forward an arbitary amount of time
        _scheduler.AdvanceBy(TimeSpan.FromSeconds(61).Ticks);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 update");
    }
}
