using System;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class DeferAnsdSkipFixture
{
    [Fact]
    public void DeferUntilLoadedDoesNothingUntilDataHasBeenReceived()
    {
        var updateReceived = false;
        IChangeSet<Person, string>? result = null;

        var cache = new SourceCache<Person, string>(p => p.Name);

        var deferStream = cache.Connect().DeferUntilLoaded().Subscribe(
            changes =>
            {
                updateReceived = true;
                result = changes;
            });

        var person = new Person("Test", 1);
        updateReceived.Should().BeFalse();
        cache.AddOrUpdate(person);

        updateReceived.Should().BeTrue();

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        result.Adds.Should().Be(1);
        result.First().Current.Should().Be(person);
        deferStream.Dispose();
    }

    [Fact]
    public void SkipInitialDoesNotReturnTheFirstBatchOfData()
    {
        var updateReceived = false;

        var cache = new SourceCache<Person, string>(p => p.Name);

        var deferStream = cache.Connect().SkipInitial().Subscribe(changes => updateReceived = true);

        updateReceived.Should().BeFalse();

        cache.AddOrUpdate(new Person("P1", 1));

        updateReceived.Should().BeFalse();

        cache.AddOrUpdate(new Person("P2", 2));
        updateReceived.Should().BeTrue();
        deferStream.Dispose();
    }
}
