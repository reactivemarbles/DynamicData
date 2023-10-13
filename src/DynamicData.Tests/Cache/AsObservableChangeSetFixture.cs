using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class AsObservableChangeSetFixture
{
    private const int MaxItems = 37;

    [Fact]
    public void ItemsFromEnumerableAreAddedToChangeSet()
    {
        // having
        var enumerable = Enumerable.Range(0, MaxItems);
        var enumObservable = Observable.Return(enumerable);

        // when
        var observableChangeSet = enumObservable.AsObservableChangeSet(i => i);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(MaxItems);
        results.Messages.Count.Should().Be(1);
    }

    [Fact]
    public void ItemsRemovedFromEnumerableAreRemovedFromChangeSet()
    {
        // having
        var enumerable1 = Enumerable.Range(0, MaxItems*2);
        var enumerable2 = Enumerable.Range(MaxItems, MaxItems);
        var enumObservable = new[] {enumerable1, enumerable2}.ToObservable();

        // when
        var observableChangeSet = enumObservable.AsObservableChangeSet(i => i);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(MaxItems);
        results.Messages.Count.Should().Be(2);
        results.Messages[0].Adds.Should().Be(MaxItems*2);
        results.Messages[1].Removes.Should().Be(MaxItems);
    }

    [Fact]
    public void ItemsUpdatedAreUpdatedInChangeSet()
    {
        // having
        var enumerable1 = Enumerable.Range(0, MaxItems * 2);
        var enumerable2 = Enumerable.Range(MaxItems, MaxItems);
        var enumObservable = new[] { enumerable1, enumerable2 }.ToObservable();

        // when
        var observableChangeSet = enumObservable.AsObservableChangeSet(i => i);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(MaxItems);
        results.Messages.Count.Should().Be(2);
        results.Messages[0].Adds.Should().Be(MaxItems * 2);
        results.Messages[1].Updates.Should().Be(MaxItems);
    }
}
