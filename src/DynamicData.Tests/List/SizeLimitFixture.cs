using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

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

    [Fact]
    public void Add()
    {
        var person = _generator.Take(1).First();
        _source.Add(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void AddLessThanLimit()
    {
        var person = _generator.Take(1).First();
        _source.Add(person);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void AddMoreThanLimit()
    {
        var people = _generator.Take(100).OrderBy(p => p.Name).ToArray();
        _source.AddRange(people);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);

        _source.Dispose();
        _results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Messages[0].Adds.Should().Be(100, "Should be 100 adds in the first update");
        _results.Messages[1].Removes.Should().Be(90, "Should be 90 removes in the second update");
    }

    [Fact]
    public void AddMoreThanLimitInBatched()
    {
        _source.AddRange(_generator.Take(10).ToArray());
        _source.AddRange(_generator.Take(10).ToArray());

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        _results.Data.Count.Should().Be(10, "Should be 10 items in the cache");
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
    public void ForceError()
    {
        var person = _generator.Take(1).First();
        Assert.Throws<ArgumentOutOfRangeException>(() => _source.RemoveAt(1));
    }

    [Fact]
    public void ThrowsIfSizeLimitIsZero() =>
        // Initialise();
        Assert.Throws<ArgumentException>(() => new SourceCache<Person, string>(p => p.Key).LimitSizeTo(0));
}
