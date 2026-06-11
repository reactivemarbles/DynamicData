// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// In-isolation tests for <see cref="MergeMany{TObject, TKey, TDestination}.Orchestrator"/>, driven
/// directly through <see cref="FakeOrchestratorContext{TKey, TInner}"/> without involving the
/// SharedDeliveryQueue runtime. Verifies the orchestrator's contract with its context (Track on Add,
/// Untrack on Remove) and its emit policy (one OnNext per inner emission, no coalescing).
/// </summary>
public sealed class MergeManyOrchestratorFixture
{
    private sealed record Item(int Id, string Name);

    [Fact]
    public void OnItemAdded_TracksInnerObservableForKey()
    {
        var context = new FakeOrchestratorContext<int, string>();
        var emitter = new CollectingObserver<string>();
        var inner = new Subject<string>();

        var orchestrator = new MergeMany<Item, int, string>.Orchestrator(
            context, emitter, (item, key) => inner);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 42, new Item(42, "alpha")) });

        context.TrackCalls.Should().HaveCount(1, "Add must produce exactly one Track call");
        context.TrackCalls[0].Key.Should().Be(42);
        context.Tracked.Should().ContainKey(42);
    }

    [Fact]
    public void OnItemRemoved_UntracksInnerObservable()
    {
        var context = new FakeOrchestratorContext<int, string>();
        var emitter = new CollectingObserver<string>();
        var orchestrator = new MergeMany<Item, int, string>.Orchestrator(
            context, emitter, (item, key) => new Subject<string>());

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 7, new Item(7, "x")) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 7, new Item(7, "x")) });

        context.UntrackCalls.Should().Equal(new[] { 7 }, "Remove must produce exactly one Untrack call");
        context.Tracked.Should().NotContainKey(7);
    }

    [Fact]
    public void OnInner_ForwardsValueToEmitterImmediately()
    {
        var context = new FakeOrchestratorContext<int, string>();
        var emitter = new CollectingObserver<string>();
        var orchestrator = new MergeMany<Item, int, string>.Orchestrator(
            context, emitter, (item, key) => new Subject<string>());

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "x")) });

        orchestrator.OnInner("first", 1);
        orchestrator.OnInner("second", 1);

        emitter.Values.Should().Equal(new[] { "first", "second" }, "MergeMany emits each inner value passthrough");
    }

    [Fact]
    public void OnDrainComplete_DoesNothingByItself()
    {
        var context = new FakeOrchestratorContext<int, string>();
        var emitter = new CollectingObserver<string>();
        var orchestrator = new MergeMany<Item, int, string>.Orchestrator(
            context, emitter, (item, key) => new Subject<string>());

        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);
        orchestrator.OnDrainComplete(isFinal: true, wasReentrant: false);

        emitter.Values.Should().BeEmpty("MergeMany does not accumulate state and therefore emits nothing at drain end");
        emitter.IsCompleted.Should().BeFalse("OnDrainComplete is not responsible for downstream completion");
    }
}
