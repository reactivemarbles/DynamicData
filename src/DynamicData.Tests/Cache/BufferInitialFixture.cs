using System;
using System.Collections.Generic;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.Cache;

public class BufferInitialFixture
{
    private static readonly ICollection<Person> People = Enumerable.Range(1, 10_000).Select(i => new Person(i.ToString(), i)).ToList();

    [Fact]
    public void BufferInitial()
    {
        var scheduler = new TestScheduler();

        using var cache = new SourceCache<Person, string>(i => i.Name);
        using var aggregator = cache.Connect().BufferInitial(TimeSpan.FromSeconds(1), scheduler).AsAggregator();
        foreach (var item in People)
        {
            cache.AddOrUpdate(item);
        }

        aggregator.Data.Count.Should().Be(0);
        aggregator.Messages.Count.Should().Be(0);

        scheduler.Start();

        aggregator.Data.Count.Should().Be(10_000);
        aggregator.Messages.Count.Should().Be(1);

        cache.AddOrUpdate(new Person("_New", 1));

        aggregator.Data.Count.Should().Be(10_001);
        aggregator.Messages.Count.Should().Be(2);
    }
}
