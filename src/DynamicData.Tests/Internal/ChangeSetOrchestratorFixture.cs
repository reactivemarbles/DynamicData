// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed class ChangeSetOrchestratorFixture : IDisposable
{
    private readonly Subject<IChangeSet<Source, int>> _source = new();
    private readonly Dictionary<int, Subject<string>> _inners = [];
    private readonly CollectingObserver<IChangeSet<string, int>> _results = new();

    private sealed record Source(int Id);

    public void Dispose()
    {
        _source.Dispose();
        foreach (var inner in _inners.Values)
            inner.Dispose();
    }

    [Fact]
    public void OnSourceNext_InvokesOnSourceChangeForEachChange()
    {
        var reasons = new List<ChangeReason>();
        using var subscription = Orchestrate((_, change) => reasons.Add(change.Reason), (cache, key, _, value) => cache.AddOrUpdate(value, key));

        _source.OnNext(new ChangeSet<Source, int> { new(ChangeReason.Add, 1, new Source(1)), new(ChangeReason.Refresh, 2, new Source(2)) });

        reasons.Should().Equal(ChangeReason.Add, ChangeReason.Refresh);
    }

    [Fact]
    public void OnItemSourceNext_RoutesValueAndFrameEndCaptures()
    {
        using var subscription = Orchestrate((_, _) => { }, (cache, key, _, value) => cache.AddOrUpdate(value, key));

        _source.OnNext(new ChangeSet<Source, int> { new(ChangeReason.Add, 1, new Source(1)) });
        _inners[1].OnNext("v");

        _results.Values.Should().ContainSingle().Which.Should().ContainSingle(c => c.Key == 1 && c.Current == "v");
    }

    [Fact]
    public void OnItemRemoved_UntracksKey()
    {
        using var subscription = Orchestrate((_, _) => { }, (_, _, _, _) => { });

        _source.OnNext(new ChangeSet<Source, int> { new(ChangeReason.Add, 1, new Source(1)) });
        _inners[1].HasObservers.Should().BeTrue();

        _source.OnNext(new ChangeSet<Source, int> { new(ChangeReason.Remove, 1, new Source(1)) });

        _inners[1].HasObservers.Should().BeFalse("removing the source item must dispose its inner subscription");
    }

    [Fact]
    public void OnFrameEnded_EmptyChangeSet_NoEmission()
    {
        using var subscription = Orchestrate((_, _) => { }, (_, _, _, _) => { });

        _source.OnNext(new ChangeSet<Source, int> { new(ChangeReason.Add, 1, new Source(1)) });

        _results.Values.Should().BeEmpty("a frame that leaves the ChangeAwareCache unchanged must not emit");
    }

    private IDisposable Orchestrate(Action<ChangeAwareCache<string, int>, Change<Source, int>> onSourceChange, Action<ChangeAwareCache<string, int>, int, Source, string> onItemSourceNext) =>
        _source
            .OrchestrateChangeSets(
                innerFactory: (_, key) => _inners[key] = new Subject<string>(),
                onSourceChange,
                onItemSourceNext)
            .Subscribe(_results);
}
