// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq;
using System.Reactive.Linq;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// In-isolation tests for <see cref="IntObservableCacheEx.MergedOrchestrator{TSource, TKey, TDest, TDestKey}"/>,
/// the orchestrator behind <c>OrchestrateManyChangeSets</c>.
/// </summary>
public sealed class MergedOrchestratorFixture
{
    private sealed record Item(int Id, string Tag);

    private static IntObservableCacheEx.MergedOrchestrator<Item, int, string, string> Build(
            FakeOrchestratorContext<int, IChangeSet<string, string>> context,
            CollectingObserver<IChangeSet<string, string>> emitter,
            bool reevalOnRefresh = false) =>
        new(context, emitter,
            changeSetSelector: (item, key) => Observable.Empty<IChangeSet<string, string>>(),
            equalityComparer: null,
            comparer: null,
            reevalOnRefresh: reevalOnRefresh);

    [Fact]
    public void OnItemAdded_TracksAndRoutesInnerChangeSet()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")) });
        orchestrator.OnInner(new ChangeSet<string, string> { new(ChangeReason.Add, "key-1", "v") }, 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        context.TrackCalls.Should().HaveCount(1);
        emitter.Values.Should().HaveCount(1);
        emitter.Values[0].Should().ContainSingle(c => c.Key == "key-1");
    }

    [Fact]
    public void OnItemRemoved_UntracksAndDropsItemsFromTracker()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1, "a")) });

        context.UntrackCalls.Should().Contain(1, "Remove must propagate as Untrack on the orchestrator's context");
    }

    [Fact]
    public void OnItemUpdated_TracksNewInnerObservable()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "first")) });
        var preTrackCount = context.TrackCalls.Count;

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Update, 1, new Item(1, "second"), new Item(1, "first")) });

        context.TrackCalls.Count.Should().Be(preTrackCount + 1, "Update must re-Track with the new item's inner observable");
    }

    [Fact]
    public void OnItemRefreshed_WithoutReevalOnRefresh_NoEmission()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = Build(context, emitter, reevalOnRefresh: false);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")) });
        orchestrator.OnInner(new ChangeSet<string, string> { new(ChangeReason.Add, "key-a", "v") }, 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        var preCount = emitter.Values.Count;

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Refresh, 1, new Item(1, "a")) });
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        emitter.Values.Count.Should().Be(preCount, "Refresh with reevalOnRefresh=false must not emit");
    }

    [Fact]
    public void MultipleKeys_TrackedIndependently()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int>
        {
            new(ChangeReason.Add, 1, new Item(1, "a")),
            new(ChangeReason.Add, 2, new Item(2, "b")),
        });

        context.TrackCalls.Select(t => t.Key).Should().BeEquivalentTo(new[] { 1, 2 });
        context.Tracked.Keys.Should().BeEquivalentTo(new[] { 1, 2 });

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1, "a")) });

        context.UntrackCalls.Should().Equal(new[] { 1 });
        context.Tracked.Keys.Should().Equal(new[] { 2 }, "Remove on key 1 must not affect key 2");
    }
}
