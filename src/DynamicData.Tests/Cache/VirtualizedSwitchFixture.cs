using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;

namespace ExternalConsumerTests.Cache;

public class VirtualizedSwitchFixture
{
    [Fact]
    public void SwitchingAwayFromVirtualizedSourceRemovesItsItems()
    {
        using var enabled = new BehaviorSubject<bool>(true);
        using var source = new SourceCache<int, int>(value => value);
        source.AddOrUpdate(Enumerable.Range(0, 20));

        IObservable<IChangeSet<int, int, VirtualContext<int>>> virtualized = CreateVirtualized(source, 10);
        IObservable<IObservable<IChangeSet<int, int, VirtualContext<int>>>> sources = enabled.Select(
            isEnabled => isEnabled ? virtualized : Observable.Empty<IChangeSet<int, int, VirtualContext<int>>>());
        IObservable<IChangeSet<int, int>> switched = sources.Switch();
        using var subscription = switched
            .ValidateChangeSets(static value => value)
            .RecordCacheItems(out var results);

        results.RecordedChangeSets.Should().ContainSingle();
        results.RecordedChangeSets[0].Adds.Should().Be(10);
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo(Enumerable.Range(0, 10));

        enabled.OnNext(false);

        results.RecordedChangeSets.Should().HaveCount(2);
        results.RecordedChangeSets[1].Removes.Should().Be(10);
        results.RecordedItemsByKey.Should().BeEmpty();
    }

    [Fact]
    public void SwitchingBetweenVirtualizedSourcesReplacesVisibleItems()
    {
        using var first = new SourceCache<int, int>(value => value);
        using var second = new SourceCache<int, int>(value => value);
        first.AddOrUpdate(Enumerable.Range(0, 10));
        second.AddOrUpdate(Enumerable.Range(100, 10));

        IObservable<IChangeSet<int, int, VirtualContext<int>>> firstVirtualized = CreateVirtualized(first, 3);
        IObservable<IChangeSet<int, int, VirtualContext<int>>> secondVirtualized = CreateVirtualized(second, 3);
        using var sources = new BehaviorSubject<IObservable<IChangeSet<int, int, VirtualContext<int>>>>(firstVirtualized);
        IObservable<IChangeSet<int, int>> switched = sources.Switch();
        using var subscription = switched
            .ValidateChangeSets(static value => value)
            .RecordCacheItems(out var results);

        results.RecordedItemsByKey.Values.Should().BeEquivalentTo([0, 1, 2]);

        sources.OnNext(secondVirtualized);

        results.RecordedChangeSets.Should().HaveCount(3);
        results.RecordedChangeSets[1].Removes.Should().Be(3);
        results.RecordedChangeSets[2].Adds.Should().Be(3);
        results.RecordedItemsByKey.Values.Should().BeEquivalentTo([100, 101, 102]);
    }

    private static IObservable<IChangeSet<int, int, VirtualContext<int>>> CreateVirtualized(SourceCache<int, int> source, int size) =>
        source.Connect().SortAndVirtualize(
            Comparer<int>.Default,
            Observable.Return<IVirtualRequest>(new VirtualRequest(0, size)));
}
