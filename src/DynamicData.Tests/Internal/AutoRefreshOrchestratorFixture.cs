// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.Internal;

public sealed class AutoRefreshOrchestratorFixture
{
    private sealed record Item(int Id);

    [Fact]
    public void OnDrainComplete_Unbuffered_FlushesPendingRefreshes()
    {
        var context = new FakeOrchestratorContext<int, Change<Item, int>>();
        var emitter = new CollectingObserver<IChangeSet<Item, int>>();
        var orchestrator = new AutoRefresh<Item, int, Unit>.Orchestrator(
            context, emitter,
            reEvaluator: (item, key) => new Subject<Unit>(),
            buffer: null,
            scheduler: null);

        var item = new Item(1);
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, item) });
        emitter.Values.Should().HaveCount(1);

        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        orchestrator.OnInner(new Change<Item, int>(ChangeReason.Refresh, 1, item), 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        emitter.Values.Should().HaveCount(2, "unbuffered drain should flush the pending refresh");
        emitter.Values[1].Should().ContainSingle(c => c.Reason == ChangeReason.Refresh && c.Key == 1);
    }

    [Fact]
    public void OnInner_KeyInSourceTouched_SuppressesRefresh()
    {
        var context = new FakeOrchestratorContext<int, Change<Item, int>>();
        var emitter = new CollectingObserver<IChangeSet<Item, int>>();
        var orchestrator = new AutoRefresh<Item, int, Unit>.Orchestrator(
            context, emitter,
            reEvaluator: (item, key) => new Subject<Unit>(),
            buffer: null,
            scheduler: null);

        var item = new Item(1);
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, item) });
        orchestrator.OnInner(new Change<Item, int>(ChangeReason.Refresh, 1, item), 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        emitter.Values.Should().HaveCount(1, "the synchronous inner refresh on a source-touched key must be suppressed");
        emitter.Values[0].Should().ContainSingle(c => c.Reason == ChangeReason.Add);
    }

    [Fact]
    public void OnDrainComplete_Buffered_NoFlushUntilTimer()
    {
        var context = new FakeOrchestratorContext<int, Change<Item, int>>();
        var emitter = new CollectingObserver<IChangeSet<Item, int>>();
        var scheduler = new TestScheduler();
        var orchestrator = new AutoRefresh<Item, int, Unit>.Orchestrator(
            context, emitter,
            reEvaluator: (item, key) => new Subject<Unit>(),
            buffer: TimeSpan.FromSeconds(10),
            scheduler: scheduler);

        var item = new Item(1);
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, item) });
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        var preInnerCount = emitter.Values.Count;
        orchestrator.OnInner(new Change<Item, int>(ChangeReason.Refresh, 1, item), 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        emitter.Values.Count.Should().Be(preInnerCount,
            "buffered drain should defer the refresh to the timer; no flush yet");
    }

    [Fact]
    public void OnDrainComplete_BufferedWithIsFinal_FlushesSynchronously()
    {
        var context = new FakeOrchestratorContext<int, Change<Item, int>>();
        var emitter = new CollectingObserver<IChangeSet<Item, int>>();
        var scheduler = new TestScheduler();
        var orchestrator = new AutoRefresh<Item, int, Unit>.Orchestrator(
            context, emitter,
            reEvaluator: (item, key) => new Subject<Unit>(),
            buffer: TimeSpan.FromSeconds(10),
            scheduler: scheduler);

        var item = new Item(1);
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, item) });
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        var preInnerCount = emitter.Values.Count;
        orchestrator.OnInner(new Change<Item, int>(ChangeReason.Refresh, 1, item), 1);
        orchestrator.OnDrainComplete(isFinal: true, wasReentrant: false);

        emitter.Values.Count.Should().Be(preInnerCount + 1,
            "isFinal must force a synchronous flush of pending refreshes even when buffered");
        emitter.Values[preInnerCount].Should().ContainSingle(c => c.Reason == ChangeReason.Refresh && c.Key == 1);
    }

    [Fact]
    public void OnItemRemoved_DropsPendingRefreshForKey()
    {
        var context = new FakeOrchestratorContext<int, Change<Item, int>>();
        var emitter = new CollectingObserver<IChangeSet<Item, int>>();
        var orchestrator = new AutoRefresh<Item, int, Unit>.Orchestrator(
            context, emitter,
            reEvaluator: (item, key) => new Subject<Unit>(),
            buffer: null,
            scheduler: null);

        var item = new Item(1);
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, item) });
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        var preInnerCount = emitter.Values.Count;
        orchestrator.OnInner(new Change<Item, int>(ChangeReason.Refresh, 1, item), 1);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, item) });
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        context.UntrackCalls.Should().Contain(1);
        var refreshCount = 0;
        foreach (var cs in emitter.Values)
        {
            foreach (var change in cs)
            {
                if (change.Reason == ChangeReason.Refresh)
                    refreshCount++;
            }
        }

        refreshCount.Should().Be(0, "a Refresh whose value is obsoleted by a Remove must never emit");
    }

    [Fact]
    public void OnItemRefreshed_DropsPendingRefreshForKey()
    {
        var context = new FakeOrchestratorContext<int, Change<Item, int>>();
        var emitter = new CollectingObserver<IChangeSet<Item, int>>();
        var scheduler = new TestScheduler();
        var orchestrator = new AutoRefresh<Item, int, Unit>.Orchestrator(
            context, emitter,
            reEvaluator: (item, key) => new Subject<Unit>(),
            buffer: TimeSpan.FromSeconds(10),
            scheduler: scheduler);

        var item = new Item(1);
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, item) });
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        orchestrator.OnInner(new Change<Item, int>(ChangeReason.Refresh, 1, item), 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        var preSourceRefreshCount = emitter.Values.Count;

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Refresh, 1, item) });
        orchestrator.OnDrainComplete(isFinal: true, wasReentrant: false);

        emitter.Values.Count.Should().Be(preSourceRefreshCount + 1,
            "the source's Refresh subsumes the pending one; flushing it would be redundant");
        emitter.Values[preSourceRefreshCount].Should().ContainSingle(c => c.Reason == ChangeReason.Refresh && c.Key == 1);
    }
}
