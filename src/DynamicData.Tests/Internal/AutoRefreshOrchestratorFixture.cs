// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed class AutoRefreshOrchestratorFixture : IDisposable
{
    private static readonly TimeSpan BufferWindow = TimeSpan.FromSeconds(10);

    private readonly Subject<IChangeSet<Item, int>> _source = new();
    private readonly Dictionary<int, Subject<Unit>> _reevaluators = [];
    private readonly CollectingObserver<IChangeSet<Item, int>> _results = new();
    private readonly TestScheduler _scheduler = new();
    private readonly Item _item = new(1);

    private sealed record Item(int Id);

    public void Dispose()
    {
        _source.Dispose();
        foreach (var reevaluator in _reevaluators.Values)
            reevaluator.Dispose();
    }

    [Fact]
    public void OnFrameEnded_Unbuffered_FlushesPendingRefreshes()
    {
        using var subscription = AutoRefresh(buffer: null).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, _item) });
        _reevaluators[1].OnNext(Unit.Default);

        _results.Values.Should().HaveCount(2, "the frame of an unbuffered reevaluation must flush its refresh");
        _results.Values[1].Should().ContainSingle(c => c.Reason == ChangeReason.Refresh && c.Key == 1);
    }

    [Fact]
    public void OnItemSourceNext_KeyTouchedBySourceInSameFrame_SuppressesRefresh()
    {
        var reevaluator = Observable.Return(Unit.Default).Concat(Observable.Never<Unit>());
        using var subscription = new AutoRefresh<Item, int, Unit>(_source, (_, _) => reevaluator).Run().Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, _item) });

        _results.Values.Should().ContainSingle("the synchronous reevaluation on subscribe falls in the Add's frame and must be suppressed")
            .Which.Should().ContainSingle(c => c.Reason == ChangeReason.Add);
    }

    [Fact]
    public void OnFrameEnded_Buffered_NoFlushUntilTimer()
    {
        using var subscription = AutoRefresh(BufferWindow).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, _item) });
        _reevaluators[1].OnNext(Unit.Default);

        _results.Values.Should().HaveCount(1, "a buffered refresh waits for the timer");

        _scheduler.AdvanceBy(BufferWindow.Ticks);

        _results.Values.Should().HaveCount(2);
        _results.Values[1].Should().ContainSingle(c => c.Reason == ChangeReason.Refresh && c.Key == 1);
    }

    [Fact]
    public void OnFrameEnded_BufferedWithIsFinal_FlushesSynchronously()
    {
        using var subscription = AutoRefresh(BufferWindow).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, _item) });
        _reevaluators[1].OnNext(Unit.Default);
        _source.OnCompleted();
        _reevaluators[1].OnCompleted();

        _results.Values.Should().HaveCount(2, "the final frame must flush pending refreshes even when buffered, since completion cancels the timer");
        _results.Values[1].Should().ContainSingle(c => c.Reason == ChangeReason.Refresh && c.Key == 1);
        _results.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void OnItemRemoved_DropsPendingRefreshForKey()
    {
        using var subscription = AutoRefresh(BufferWindow).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, _item) });
        _reevaluators[1].OnNext(Unit.Default);
        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, _item) });
        _scheduler.AdvanceBy(BufferWindow.Ticks);

        _reevaluators[1].HasObservers.Should().BeFalse();
        _results.Values.SelectMany(changes => changes).Should().NotContain(c => c.Reason == ChangeReason.Refresh, "a Refresh whose value is obsoleted by a Remove must never emit");
    }

    [Fact]
    public void OnItemRefreshed_DropsPendingRefreshForKey()
    {
        using var subscription = AutoRefresh(BufferWindow).Subscribe(_results);

        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, _item) });
        _reevaluators[1].OnNext(Unit.Default);
        _source.OnNext(new ChangeSet<Item, int> { new(ChangeReason.Refresh, 1, _item) });
        _scheduler.AdvanceBy(BufferWindow.Ticks);

        _results.Values.Should().HaveCount(2, "the source's Refresh subsumes the pending one; flushing it would be redundant");
        _results.Values[1].Should().ContainSingle(c => c.Reason == ChangeReason.Refresh && c.Key == 1);
    }

    private IObservable<IChangeSet<Item, int>> AutoRefresh(TimeSpan? buffer) =>
        new AutoRefresh<Item, int, Unit>(_source, (_, key) => _reevaluators[key] = new Subject<Unit>(), buffer, _scheduler).Run();
}
