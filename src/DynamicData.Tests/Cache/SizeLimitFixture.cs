using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class SizeLimitFixture : IDisposable
{
    private readonly RandomPersonGenerator _generator = new();

    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly TestScheduler _scheduler;

    private readonly IDisposable _sizeLimiter;

    private readonly ISourceCache<Person, string> _source;

    public SizeLimitFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceCache<Person, string>(p => p.Key);
        _sizeLimiter = _source.LimitSizeTo(10, _scheduler).Subscribe();
        _results = _source.Connect().AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        var person = _generator.Take(1).First();
        _source.AddOrUpdate(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task AddLessThanLimit()
    {
        var person = _generator.Take(1).First();
        _source.AddOrUpdate(person);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");
        await Assert.That(_results.Data.Items[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task AddMoreThanLimit()
    {
        var people = _generator.Take(100).OrderBy(p => p.Name).ToArray();
        _source.AddOrUpdate(people);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

        _source.Dispose();
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(100).Because("Should be 100 adds in the first update");
        await Assert.That(_results.Messages[1].Removes).IsEqualTo(90).Because("Should be 90 removes in the second update");
    }

    [Test]
    public async Task AddMoreThanLimitInBatched()
    {
        // _generator.Take(N) draws random Person rows from a finite name pool; a second
        // Take(10) call can produce keys that collide with the first batch, turning an
        // Add into an Update and breaking the per-message Adds count. Draw a larger pool
        // up front, dedupe by Key, then split into two non-overlapping batches of 10.
        var people = _generator.Take(60).DistinctBy(p => p.Key).Take(20).ToArray();
        _source.AddOrUpdate(people.Take(10).ToArray());
        _source.AddOrUpdate(people.Skip(10).Take(10).ToArray());

        _scheduler.Start();

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
    public async Task InvokeLimitSizeToWhenOverLimit()
    {
        var removesTriggered = false;
        var subscriber = _source.LimitSizeTo(10, _scheduler).Subscribe(removes => { removesTriggered = true; });

        // _generator.Take(N) draws random Person rows from a finite name pool; a second
        // Take(10) call can produce keys that collide with the first batch, turning an
        // Add into an Update and breaking the per-message Adds count. Draw a larger pool
        // up front, dedupe by Key, then split into two non-overlapping batches of 10.
        var people = _generator.Take(60).DistinctBy(p => p.Key).Take(20).ToArray();
        _source.AddOrUpdate(people.Take(10).ToArray());
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        await Assert.That(removesTriggered).IsFalse();

        _source.AddOrUpdate(people.Skip(10).Take(10).ToArray());

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        await Assert.That(removesTriggered).IsTrue();

        await Assert.That(_results.Messages.Count).IsEqualTo(3).Because("Should be 3 updates");
        await Assert.That(_results.Messages[0].Adds).IsEqualTo(10).Because("Should be 10 adds in the first update");
        await Assert.That(_results.Messages[1].Adds).IsEqualTo(10).Because("Should be 10 adds in the second update");
        await Assert.That(_results.Messages[2].Removes).IsEqualTo(10).Because("Should be 10 removes in the third update");

        subscriber.Dispose();
    }

    [Test]
    public async Task OnCompleteIsInvokedWhenSourceIsDisposed()
    {
        var completions = 0;
        using var subscriber = _source.LimitSizeTo(10, _scheduler)
            .Subscribe(_ => { }, () => completions++);

        _source.Dispose();
        await Assert.That(completions).IsEqualTo(0);

        _scheduler.Start();
        await Assert.That(completions).IsEqualTo(1);
    }

    [Test]
    public async Task DisposingSubscriptionDoesNotCompleteObserver()
    {
        var completions = 0;
        var subscriber = _source.LimitSizeTo(10, _scheduler)
            .Subscribe(_ => { }, () => completions++);

        subscriber.Dispose();
        _source.Dispose();
        _scheduler.Start();

        await Assert.That(completions).IsEqualTo(0);
    }

    [Test]
    public async Task SourceErrorIsForwardedOnTheSchedulerWithoutCompletion()
    {
        using var source = new TestSourceCache<int, int>(value => value);
        var scheduler = new TestScheduler();
        var expected = new InvalidOperationException("Source failed");
        Exception? observed = null;
        var completions = 0;
        using var subscription = source.LimitSizeTo(1, scheduler)
            .Subscribe(_ => { }, error => observed = error, () => completions++);

        source.SetError(expected);
        await Assert.That(observed).IsNull();
        scheduler.Start();

        await Assert.That(observed).IsSameReferenceAs(expected);
        await Assert.That(completions).IsEqualTo(0);
    }
    [Test]
    public async Task ThrowsIfSizeLimitIsZero() =>
        // Initialise();
        await Assert.That(() => new SourceCache<Person, string>(p => p.Key).LimitSizeTo(0)).Throws<ArgumentException>();
}
