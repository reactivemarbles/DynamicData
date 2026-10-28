// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed class MergedOrchestratorFixture : IDisposable
{
    private readonly Subject<IChangeSet<Item, int>> _source = new();
    private readonly Dictionary<int, Subject<IChangeSet<string, string>>> _inners = [];
    private readonly CollectingObserver<IChangeSet<string, string>> _results = new();
    private int _selectorCalls;

    private sealed record Item(int Id, string Tag);

    public void Dispose()
    {
        _source.Dispose();
        foreach (var inner in _inners.Values)
            inner.Dispose();
    }

    [Fact]
    public void OnItemAdded_TracksAndRoutesInnerChangeSet()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")) });
        _inners[1].OnNext(new ChangeSet<string, string> { new(ChangeReason.Add, "key-1", "v") });

        _selectorCalls.Should().Be(1);
        _results.Values.Should().ContainSingle().Which.Should().ContainSingle(c => c.Key == "key-1");
    }

    [Fact]
    public void OnItemRemoved_UntracksAndDropsItemsFromTracker()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")) });
        _inners[1].OnNext(new ChangeSet<string, string> { new(ChangeReason.Add, "key-1", "v") });
        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1, "a")) });

        _inners[1].HasObservers.Should().BeFalse("removing the source item must dispose its inner subscription");
        _results.Values.Should().HaveCount(2);
        _results.Values[1].Should().ContainSingle(c => c.Key == "key-1" && c.Reason == ChangeReason.Remove);
    }

    [Fact]
    public void OnItemUpdated_TracksNewInnerObservable()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "first")) });
        var first = _inners[1];

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Update, 1, new Item(1, "second"), new Item(1, "first")) });

        _selectorCalls.Should().Be(2, "Update must re-track with the new item's inner observable");
        first.HasObservers.Should().BeFalse("the replaced inner subscription must be disposed");
        _inners[1].HasObservers.Should().BeTrue();
    }

    [Fact]
    public void OnItemRefreshed_WithoutReevalOnRefresh_NoEmission()
    {
        using var subscription = Orchestrate(reevalOnRefresh: false);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")) });
        _inners[1].OnNext(new ChangeSet<string, string> { new(ChangeReason.Add, "key-a", "v") });
        var preCount = _results.Values.Count;

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Refresh, 1, new Item(1, "a")) });

        _results.Values.Count.Should().Be(preCount, "Refresh with reevalOnRefresh=false must not emit");
    }

    [Fact]
    public void MultipleKeys_TrackedIndependently()
    {
        using var subscription = Orchestrate();

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1, "a")), new(ChangeReason.Add, 2, new Item(2, "b")) });
        _inners.Keys.Should().BeEquivalentTo([1, 2]);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1, "a")) });

        _inners[1].HasObservers.Should().BeFalse();
        _inners[2].HasObservers.Should().BeTrue("Remove on key 1 must not affect key 2");
    }

    private IDisposable Orchestrate(bool reevalOnRefresh = false) =>
        _source
            .OrchestrateManyChangeSets<Item, int, string, string>(
                (_, key) =>
                {
                    _selectorCalls++;
                    return _inners[key] = new Subject<IChangeSet<string, string>>();
                },
                reevalOnRefresh: reevalOnRefresh)
            .Subscribe(_results);
}
