// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed class LambdaCacheOrchestratorFixture : IDisposable
{
    private readonly Subject<IChangeSet<Item, int>> _source = new();
    private readonly Subject<string> _inner = new();
    private readonly CollectingObserver<int> _results = new();

    private sealed record Item(int Id);

    public void Dispose()
    {
        _source.Dispose();
        _inner.Dispose();
    }

    [Fact]
    public void OnSourceNext_ForwardsChangesToLambda()
    {
        var received = new List<IChangeSet<Item, int>>();
        using var subscription = _source.OrchestrateSubscriptions<Item, int, string, int>((changes, _) => received.Add(changes), (_, _, _) => { }).Subscribe(_results);

        var changeset = new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) };
        _source.OnNext(changeset);

        received.Should().ContainSingle().Which.Should().BeSameAs(changeset);
    }

    [Fact]
    public void OnSourceNext_ForwardsOrchestrationToLambda()
    {
        var received = new List<(string Value, int Key)>();
        using var subscription = _source.OrchestrateSubscriptions<Item, int, string, int>((_, orchestration) => orchestration.Track(1, _inner), (value, key, _) => received.Add((value, key))).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        _inner.OnNext("tracked");

        received.Should().Equal([("tracked", 1)], "an inner observable tracked through the lambda's orchestration must reach onItemSourceNext");
    }

    [Fact]
    public void OnItemSourceNext_ForwardsValueAndKeyToLambda()
    {
        var received = new List<(string Value, int Key)>();
        using var subscription = _source.OrchestrateSubscriptions<Item, int, string, int>((_, orchestration) => orchestration.Track(42, _inner), (value, key, _) => received.Add((value, key))).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 42, new Item(42)) });
        _inner.OnNext("hello");

        received.Should().Equal([("hello", 42)]);
    }

    [Fact]
    public void OnItemSourceNext_ForwardsEmitterToLambda()
    {
        using var subscription = _source.OrchestrateSubscriptions<Item, int, string, int>((_, orchestration) => orchestration.Track(1, _inner), (value, _, emitter) => emitter.OnNext(value.Length)).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        _inner.OnNext("hello");

        _results.Values.Should().Equal(["hello".Length], "values emitted through onItemSourceNext's emitter must reach the subscriber");
    }

    [Fact]
    public void OnFrameEnded_ForwardsEmitterToLambda()
    {
        var frameCount = 0;
        using var subscription = _source.OrchestrateSubscriptions<Item, int, string, int>((_, _) => { }, (_, _, _) => { }, emitter => emitter.OnNext(++frameCount)).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 2, new Item(2)) });

        _results.Values.Should().Equal([1, 2], "each source notification is its own frame, and onFrameEnded's emitter must reach the subscriber");
    }
}
