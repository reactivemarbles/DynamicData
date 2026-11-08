// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed class MergedListOrchestratorFixture : IDisposable
{
    private readonly Subject<IChangeSet<Item, int>> _source = new();
    private readonly Dictionary<int, Subject<IChangeSet<string>>> _inners = [];
    private readonly CollectingObserver<IChangeSet<string>> _results = new();
    private int _selectorCalls;

    private sealed record Item(int Id);

    public void Dispose()
    {
        _source.Dispose();
        foreach (var inner in _inners.Values)
            inner.Dispose();
    }

    [Fact]
    public void OnItemAdded_TracksAndForwardsListChanges()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        _inners[1].OnNext(new ChangeSet<string> { new(ListChangeReason.Add, "value-a") });

        _selectorCalls.Should().Be(1);
        _results.Values.Should().ContainSingle().Which.Should().ContainSingle(c => c.Reason == ListChangeReason.Add && c.Item.Current == "value-a");
    }

    [Fact]
    public void OnItemRemoved_UntracksAndRemovesPriorItemsFromTracker()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        _inners[1].OnNext(new ChangeSet<string> { new(ListChangeReason.Add, "value-a") });
        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1)) });

        _inners[1].HasObservers.Should().BeFalse("removing the source item must dispose its inner subscription");
        _results.Values.Should().HaveCount(2);
        _results.Values[1].Removes.Should().Be(1, "the removed item's contribution must leave the merged list");
    }

    [Fact]
    public void OnItemUpdated_TracksNewInnerObservable()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        var first = _inners[1];

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Update, 1, new Item(1), new Item(1)) });

        _selectorCalls.Should().Be(2, "Update must re-track with the new item's inner observable");
        first.HasObservers.Should().BeFalse("the replaced inner subscription must be disposed");
    }

    [Fact]
    public void MultipleKeys_TrackedIndependently()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)), new(ChangeReason.Add, 2, new Item(2)) });
        _inners.Keys.Should().BeEquivalentTo([1, 2]);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1)) });

        _inners[1].HasObservers.Should().BeFalse();
        _inners[2].HasObservers.Should().BeTrue("Remove on key 1 must not affect key 2");
    }

    private IDisposable Orchestrate() =>
        _source
            .OrchestrateManyChangeSets<Item, int, string>((_, key) =>
            {
                _selectorCalls++;
                return _inners[key] = new Subject<IChangeSet<string>>();
            })
            .Subscribe(_results);
}
