// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// In-isolation tests for <see cref="IntObservableCacheEx.ChangeSetOrchestrator{TSource, TKey, TInner, TOutput}"/>,
/// the orchestrator behind <c>OrchestrateChangeSets</c>. Verifies that onSourceChange and onInner
/// callbacks fire for the right reasons and that drain-end captures and emits accumulated state.
/// </summary>
public sealed class ChangeSetOrchestratorFixture
{
    private sealed record Source(int Id);

    [Fact]
    public void OnSourceChangeSet_InvokesOnSourceChangeForEachChange()
    {
        var context = new FakeOrchestratorContext<int, (Source Item, string Value)>();
        var emitter = new CollectingObserver<IChangeSet<string, int>>();
        var reasons = new System.Collections.Generic.List<ChangeReason>();

        var orchestrator = new IntObservableCacheEx.ChangeSetOrchestrator<Source, int, string, string>(
            context, emitter,
            innerFactory: (item, key) => Observable.Empty<string>(),
            onSourceChange: (cache, change) => reasons.Add(change.Reason),
            onInner: (cache, key, item, value) => cache.AddOrUpdate(value, key));

        orchestrator.OnSourceChangeSet(new ChangeSet<Source, int>
        {
            new(ChangeReason.Add, 1, new Source(1)),
            new(ChangeReason.Refresh, 2, new Source(2)),
        });

        reasons.Should().Equal(new[] { ChangeReason.Add, ChangeReason.Refresh });
    }

    [Fact]
    public void OnInner_RoutesValueAndDrainCaptures()
    {
        var context = new FakeOrchestratorContext<int, (Source Item, string Value)>();
        var emitter = new CollectingObserver<IChangeSet<string, int>>();
        var source = new Source(1);

        var orchestrator = new IntObservableCacheEx.ChangeSetOrchestrator<Source, int, string, string>(
            context, emitter,
            innerFactory: (item, key) => Observable.Empty<string>(),
            onSourceChange: (cache, change) => { },
            onInner: (cache, key, item, value) => cache.AddOrUpdate(value, key));

        orchestrator.OnSourceChangeSet(new ChangeSet<Source, int> { new(ChangeReason.Add, 1, source) });
        orchestrator.OnInner((source, "v"), 1);
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        emitter.Values.Should().HaveCount(1);
        emitter.Values[0].Should().ContainSingle(c => c.Key == 1 && c.Current == "v");
    }

    [Fact]
    public void OnItemRemoved_UntracksKey()
    {
        var context = new FakeOrchestratorContext<int, (Source Item, string Value)>();
        var emitter = new CollectingObserver<IChangeSet<string, int>>();
        var orchestrator = new IntObservableCacheEx.ChangeSetOrchestrator<Source, int, string, string>(
            context, emitter,
            innerFactory: (item, key) => Observable.Empty<string>(),
            onSourceChange: (cache, change) => { },
            onInner: (cache, key, item, value) => { });

        orchestrator.OnSourceChangeSet(new ChangeSet<Source, int> { new(ChangeReason.Add, 1, new Source(1)) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Source, int> { new(ChangeReason.Remove, 1, new Source(1)) });

        context.UntrackCalls.Should().Equal(new[] { 1 });
    }

    [Fact]
    public void OnDrainComplete_EmptyChangeSet_NoEmission()
    {
        var context = new FakeOrchestratorContext<int, (Source Item, string Value)>();
        var emitter = new CollectingObserver<IChangeSet<string, int>>();
        var orchestrator = new IntObservableCacheEx.ChangeSetOrchestrator<Source, int, string, string>(
            context, emitter,
            innerFactory: (item, key) => Observable.Empty<string>(),
            // Source-change callback intentionally does nothing, so the ChangeAwareCache stays empty.
            onSourceChange: (cache, change) => { },
            onInner: (cache, key, item, value) => { });

        orchestrator.OnSourceChangeSet(new ChangeSet<Source, int> { new(ChangeReason.Add, 1, new Source(1)) });
        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        emitter.Values.Should().BeEmpty("drain end with an empty ChangeAwareCache must not emit");
    }
}
