// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// In-isolation tests for <see cref="IntObservableCacheEx.MergedOrchestrator{TSource, TKey, TDest, TDestKey}"/>,
/// the orchestrator behind <c>OrchestrateManyChangeSets</c>. Verifies per-source-key tracking, update
/// semantics (new tracked + prior items removed from tracker), and the reevalOnRefresh flag.
/// </summary>
public sealed class MergedOrchestratorFixture
{
    private sealed record Item(int Id, string Tag);

    [Fact]
    public void OnItemAdded_TracksAndRoutesInnerChangeSet()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = new IntObservableCacheEx.MergedOrchestrator<Item, int, string, string>(
            context, emitter,
            changeSetSelector: (item, key) => Observable.Empty<IChangeSet<string, string>>(),
            equalityComparer: null,
            comparer: null,
            reevalOnRefresh: false);

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
        var orchestrator = new IntObservableCacheEx.MergedOrchestrator<Item, int, string, string>(
            context, emitter,
            changeSetSelector: (item, key) => Observable.Empty<IChangeSet<string, string>>(),
            equalityComparer: null,
            comparer: null,
            reevalOnRefresh: false);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1, "a")) });

        context.UntrackCalls.Should().Contain(1, "Remove must propagate as Untrack on the orchestrator's context");
    }
}
