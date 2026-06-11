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
/// In-isolation tests for <see cref="IntObservableCacheEx.MergedListOrchestrator{TSource, TKey, TDest}"/>,
/// the cache-source-to-list-merged orchestrator. Verifies per-source-key list tracking and removal
/// semantics through the ChangeSetMergeTracker.
/// </summary>
public sealed class MergedListOrchestratorFixture
{
    private sealed record Item(int Id);

    [Fact]
    public void OnItemAdded_TracksAndForwardsListChanges()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string>>();
        var emitter = new CollectingObserver<IChangeSet<string>>();
        var orchestrator = new IntObservableCacheEx.MergedListOrchestrator<Item, int, string>(
            context, emitter,
            changeSetSelector: (item, key) => Observable.Empty<IChangeSet<string>>(),
            equalityComparer: null);

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
        var orchestrator = new IntObservableCacheEx.MergedListOrchestrator<Item, int, string>(
            context, emitter,
            changeSetSelector: (item, key) => Observable.Empty<IChangeSet<string>>(),
            equalityComparer: null);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1)) });

        context.UntrackCalls.Should().Contain(1, "Remove must propagate as Untrack on the orchestrator's context");
    }
}
