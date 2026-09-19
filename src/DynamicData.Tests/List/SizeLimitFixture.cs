using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class SizeLimitFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person> _results;

    private readonly TestScheduler _scheduler;

    private readonly IDisposable _sizeLimiter;

    private readonly ISourceList<Person> _source;

    public SizeLimitFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceList<Person>();
        _sizeLimiter = _source.LimitSizeTo(10, _scheduler).Subscribe();
        _results = _source.Connect().AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        var person = _generator.Take(1).First();
        _source.Add(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task AddLessThanLimit()
    {
        var person = _generator.Take(1).First();
        _source.Add(person);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task AddMoreThanLimit()
    {
        var people = _generator.Take(100).OrderBy(p => p.Name).ToArray();
        _source.AddRange(people);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

        _source.Dispose();
        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should be 10 items in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should be 100 adds in the first update");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(90).Because("Should be 90 removes in the second update");
    }

    [Test]
    public async Task AddMoreThanLimitInBatched()
    {
        _source.AddRange(_generator.Take(10).ToArray());
        _source.AddRange(_generator.Take(10).ToArray());

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        await Assert.That(_results.Data.Count).IsEqualTo(10).Because("Should be 10 items in the cache");
        await Assert.That(_results.Messages.Count).IsEqualTo(3).Because("Should be 3 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(10).Because("Should be 10 adds in the first update");
        await Assert.That(_results.Messages[1].Adds).IsEqualTo(10).Because("Should be 10 adds in the second update");
        await Assert.That(_results.Messages[2].Removes).IsEqualTo(10).Because("Should be 10 removes in the third update");
    }

    public void Dispose()
    {
        _sizeLimiter.Dispose();
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task ForceError()
    {
        var person = _generator.Take(1).First();
        await Assert.That(() => _source.RemoveAt(1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowsIfSizeLimitIsZero() =>
        // Initialise();
        await Assert.That(() => new SourceCache<Person, string>(p => p.Key).LimitSizeTo(0)).ThrowsExactly<ArgumentException>();
}
