using System;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

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

    [Fact]
    public void Add()
    {
        var person = _generator.Take(1).First();
        _source.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void AddLessThanLimit()
    {
        var person = _generator.Take(1).First();
        _source.AddOrUpdate(person);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void AddMoreThanLimit()
    {
        var people = _generator.Take(100).OrderBy(p => p.Name).ToArray();
        _source.AddOrUpdate(people);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

        _source.Dispose();
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(100, "Should be 100 adds in the first update");
        _results.Messages[1].Removes.Should().Be(90, "Should be 90 removes in the second update");
    }

    [Fact]
    public void AddMoreThanLimitInBatched()
    {
        _source.AddOrUpdate(_generator.Take(10).ToArray());
        _source.AddOrUpdate(_generator.Take(10).ToArray());

        _scheduler.Start();

        _results.Messages.Count.Should().Be(3, "Should be 3 updates");
        _results.Messages[0].Adds.Should().Be(10, "Should be 10 adds in the first update");
        _results.Messages[1].Adds.Should().Be(10, "Should be 10 adds in the second update");
        _results.Messages[2].Removes.Should().Be(10, "Should be 10 removes in the third update");
    }

    public void Dispose()
    {
        _sizeLimiter.Dispose();
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void InvokeLimitSizeToWhenOverLimit()
    {
        var removesTriggered = false;
        var subscriber = _source.LimitSizeTo(10, _scheduler).Subscribe(removes => { removesTriggered = true; });

        _source.AddOrUpdate(_generator.Take(10).ToArray());
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        removesTriggered.Should().BeFalse();

        _source.AddOrUpdate(_generator.Take(10).ToArray());

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        removesTriggered.Should().BeTrue();

        _results.Messages.Count.Should().Be(3, "Should be 3 updates");
        _results.Messages[0].Adds.Should().Be(10, "Should be 10 adds in the first update");
        _results.Messages[1].Adds.Should().Be(10, "Should be 10 adds in the second update");
        _results.Messages[2].Removes.Should().Be(10, "Should be 10 removes in the third update");

        subscriber.Dispose();
    }

    [Fact(Skip = "Need to re-examine and fix failure")]
    public void OnCompleteIsInvokedWhenSourceIsDisposed()
    {
        var completed = false;

        var subscriber = _source.LimitSizeTo(10).Finally(() => completed = true).Subscribe(updates => { Console.WriteLine(); });

        _source.Dispose();

        completed.Should().BeTrue();
    }

    [Fact]
    public void ThrowsIfSizeLimitIsZero() =>
        // Initialise();
        Assert.Throws<ArgumentException>(() => new SourceCache<Person, string>(p => p.Key).LimitSizeTo(0));
}
