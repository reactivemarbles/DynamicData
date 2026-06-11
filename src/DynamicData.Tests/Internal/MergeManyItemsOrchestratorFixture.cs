// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Subjects;

using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// In-isolation tests for <see cref="MergeManyItems{TObject, TKey, TDestination}.Orchestrator"/>.
/// Differs from MergeMany in that emissions are wrapped with the source item, producing
/// <see cref="ItemWithValue{TObject, TValue}"/>.
/// </summary>
public sealed class MergeManyItemsOrchestratorFixture
{
    private sealed record Item(int Id, string Name);

    [Fact]
    public void OnInner_EmitsItemWithValuePairing()
    {
        var context = new FakeOrchestratorContext<int, (Item Item, string Value)>();
        var emitter = new CollectingObserver<ItemWithValue<Item, string>>();
        var item = new Item(1, "alpha");

        var orchestrator = new MergeManyItems<Item, int, string>.Orchestrator(
            context, emitter, (i, k) => new Subject<string>());

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, item) });

        orchestrator.OnInner((item, "hello"), 1);

        emitter.Values.Should().HaveCount(1);
        emitter.Values[0].Item.Should().BeSameAs(item);
        emitter.Values[0].Value.Should().Be("hello");
    }

    [Fact]
    public void OnItemRemoved_UntracksKey()
    {
        var context = new FakeOrchestratorContext<int, (Item Item, string Value)>();
        var emitter = new CollectingObserver<ItemWithValue<Item, string>>();
        var orchestrator = new MergeManyItems<Item, int, string>.Orchestrator(
            context, emitter, (i, k) => new Subject<string>());

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "x")) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1, "x")) });

        context.UntrackCalls.Should().Equal(new[] { 1 });
    }
}
