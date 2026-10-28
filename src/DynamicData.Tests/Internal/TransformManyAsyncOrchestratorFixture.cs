// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed class TransformManyAsyncOrchestratorFixture : IDisposable
{
    private readonly Subject<IChangeSet<Item, int>> _source = new();
    private readonly Dictionary<int, Subject<IChangeSet<string, string>>> _inners = [];
    private readonly CollectingObserver<IChangeSet<string, string>> _results = new();

    private sealed record Item(int Id);

    public void Dispose()
    {
        _source.Dispose();
        foreach (var inner in _inners.Values)
            inner.Dispose();
    }

    [Fact]
    public void OnItemAdded_TracksTransformedObservable()
    {
        using var subscription = Transform().Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });

        _inners.Keys.Should().Equal(1);
        _inners[1].HasObservers.Should().BeTrue();
    }

    [Fact]
    public void OnItemRemoved_UntracksAndDropsFromTracker()
    {
        using var subscription = Transform().Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        _inners[1].OnNext(new ChangeSet<string, string> { new(ChangeReason.Add, "x", "value-x") });
        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1)) });

        _inners[1].HasObservers.Should().BeFalse();
        _results.Values.Should().HaveCount(2);
        _results.Values[1].Should().ContainSingle(c => c.Key == "x" && c.Reason == ChangeReason.Remove);
    }

    [Fact]
    public void OnFrameEnded_EmitsAccumulatedTrackerState()
    {
        using var subscription = Transform().Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        _inners[1].OnNext(new ChangeSet<string, string> { new(ChangeReason.Add, "x", "value-x") });

        _results.Values.Should().ContainSingle().Which.Should().ContainSingle(c => c.Key == "x");
    }

    [Fact]
    public void TransformerThrows_RoutesThroughErrorHandler()
    {
        Error<Item, int>? capturedError = null;
        var transform = new TransformManyAsync<Item, int, string, string>(_source, (_, _) => throw new InvalidOperationException("transformer-broke"), null, null, error => capturedError = error);
        using var subscription = transform.Run().Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });

        capturedError.Should().NotBeNull();
        capturedError!.Exception!.Message.Should().Be("transformer-broke");
        _results.Error.Should().BeNull("a handled transformer error must not terminate the stream");
    }

    private IObservable<IChangeSet<string, string>> Transform() =>
        new TransformManyAsync<Item, int, string, string>(_source, (_, key) => Task.FromResult<IObservable<IChangeSet<string, string>>>(_inners[key] = new Subject<IChangeSet<string, string>>()), null, null).Run();
}
