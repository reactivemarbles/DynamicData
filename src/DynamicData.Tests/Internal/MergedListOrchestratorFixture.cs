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

public sealed class MergedListOrchestratorFixture
{
    private sealed record Item(int Id);

    private static IntObservableCacheEx.MergedListOrchestrator<Item, int, string> Build(
            FakeOrchestratorContext<int, IChangeSet<string>> context,
            CollectingObserver<IChangeSet<string>> emitter) =>
        new(context, emitter,
            changeSetSelector: (item, key) => Observable.Empty<IChangeSet<string>>(),
            equalityComparer: null);

    [Fact]
    public void OnItemAdded_TracksAndForwardsListChanges()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string>>();
        var emitter = new CollectingObserver<IChangeSet<string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        orchestrator.OnInner(new ChangeSet<string> { new(ListChangeReason.Add, "value-a") }, 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        context.TrackCalls.Should().HaveCount(1);
        emitter.Values.Should().HaveCount(1);
    }

    [Fact]
    public void OnItemRemoved_UntracksAndRemovesPriorItemsFromTracker()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string>>();
        var emitter = new CollectingObserver<IChangeSet<string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1)) });

        context.UntrackCalls.Should().Contain(1, "Remove must propagate as Untrack on the orchestrator's context");
    }

    [Fact]
    public void OnItemUpdated_TracksNewInnerObservable()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string>>();
        var emitter = new CollectingObserver<IChangeSet<string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        var preTrackCount = context.TrackCalls.Count;

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Update, 1, new Item(1), new Item(1)) });

        context.TrackCalls.Count.Should().Be(preTrackCount + 1, "Update must re-Track with the new item's inner observable");
    }

    [Fact]
    public void MultipleKeys_TrackedIndependently()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string>>();
        var emitter = new CollectingObserver<IChangeSet<string>>();
        var orchestrator = Build(context, emitter);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int>
        {
            new(ChangeReason.Add, 1, new Item(1)),
            new(ChangeReason.Add, 2, new Item(2)),
        });

        context.TrackCalls.Select(t => t.Key).Should().BeEquivalentTo(new[] { 1, 2 });

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1)) });

        context.UntrackCalls.Should().Equal(new[] { 1 });
        context.Tracked.Keys.Should().Equal(new[] { 2 }, "Remove on key 1 must not affect key 2");
    }
}
