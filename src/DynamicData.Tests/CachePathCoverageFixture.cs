#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif

namespace DynamicData.Tests;

public class CachePathCoverageFixture
{
    [Test]
    public async Task VirtualChangeSetsCompareTheirWindowAndSortedSnapshot()
    {
        var sorted = VirtualChangeSet<int, int>.Empty.SortedItems;
        var first = new VirtualChangeSet<int, int>(Array.Empty<Change<int, int>>(), sorted, new VirtualResponse(2, 0, 5));
        var equal = new VirtualChangeSet<int, int>(Array.Empty<Change<int, int>>(), sorted, new VirtualResponse(2, 0, 5));
        var otherWindow = new VirtualChangeSet<int, int>(Array.Empty<Change<int, int>>(), sorted, new VirtualResponse(2, 1, 5));

        await Assert.That(first.Equals(first)).IsTrue();
        await Assert.That(first == equal).IsTrue();
        await Assert.That(first.Equals((object)equal)).IsTrue();
        await Assert.That(first.GetHashCode()).IsEqualTo(equal.GetHashCode());
        await Assert.That(first != otherWindow).IsTrue();
        await Assert.That(first.Equals((VirtualChangeSet<int, int>?)null)).IsFalse();
        await Assert.That(first.Equals(new object())).IsFalse();
    }

    [Test]
    public async Task SizeExpirerCapsTheResultAndForwardsCompletion()
    {
        using var source = new SourceCache<int, int>(value => value);
        using var results = new SizeExpirer<int, int>(source.Connect(), 2).Run().AsAggregator();
        source.AddOrUpdate(new[] { 1, 2, 3, 4 });
        await Assert.That(results.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Items.All(value => value is >= 1 and <= 4)).IsTrue();
        source.Dispose();
        await Assert.That(results.IsCompleted).IsTrue();
        await Assert.That(() => new SizeExpirer<int, int>(source.Connect(), 0)).Throws<ArgumentException>();
        await Assert.That(() => new SizeExpirer<int, int>(null!, 1)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task VirtualAggregatorRecordsErrorsAndDisposesItsCache()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IVirtualChangeSet<int, int>>();
        using var results = new VirtualChangeSetAggregator<int, int>(source);
        source.OnNext(VirtualChangeSet<int, int>.Empty);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(results.Summary).IsNotNull();
        var error = new InvalidOperationException("expected");
        source.OnError(error);
        await Assert.That(results.Error).IsSameReferenceAs(error);
        results.Dispose();
        results.Dispose();
        await Assert.That(source.HasObservers).IsFalse();
    }

    [Test]
    public async Task QueryTriggerReflectsCurrentStateAndStopsAfterRemoval()
    {
        using var source = new SourceCache<int, int>(value => value);
        using var trigger = new ReactiveUI.Primitives.Signals.Signal<int>();
        var snapshots = new List<int[]>();
        using var subscription = source.Connect().QueryWhenChanged<int, int, int>(_ => trigger)
            .Subscribe(query => snapshots.Add(query.Items.Order().ToArray()));
        source.AddOrUpdate(1);
        trigger.OnNext(0);
        await Assert.That(snapshots.Count).IsEqualTo(2);
        await Assert.That(snapshots.All(snapshot => snapshot.SequenceEqual(new[] { 1 }))).IsTrue();
        source.RemoveKey(1);
        trigger.OnNext(0);
        await Assert.That(snapshots.Count).IsEqualTo(3);
        await Assert.That(snapshots[^1]).IsEmpty();
        await Assert.That(trigger.HasObservers).IsFalse();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task FinallyRunsExactlyOnceAfterTerminationOrDisposal(int termination)
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<int>();
        var events = new List<string>();
        var error = new InvalidOperationException("expected");
        Exception? receivedError = null;
        using var subscription = new FinallySafe<int>(source, () => events.Add("finally")).Run().Subscribe(
            value => events.Add(value.ToString()),
            failure => { receivedError = failure; events.Add("error"); },
            () => events.Add("completed"));

        source.OnNext(7);
        if (termination == 0)
            source.OnCompleted();
        else if (termination == 1)
            source.OnError(error);
        subscription.Dispose();
        subscription.Dispose();
        source.OnNext(9);

        var expected = termination switch
        {
            0 => new[] { "7", "completed", "finally" },
            1 => new[] { "7", "error", "finally" },
            _ => new[] { "7", "finally" }
        };
        await Assert.That(events.SequenceEqual(expected)).IsTrue();
        await Assert.That(receivedError).IsSameReferenceAs(termination == 1 ? error : null);
        await Assert.That(source.HasObservers).IsFalse();
    }

    [Test]
    public async Task FilteringRefreshesAddsUpdatesAndRemovesOnlyMatchingItems()
    {
        var filtered = new ChangeAwareCache<int, int>();
        filtered.FilterChanges(new ChangeSet<int, int>
        {
            new(ChangeReason.Add, 1, 2),
            new(ChangeReason.Add, 2, 3),
            new(ChangeReason.Add, 3, 4)
        }, value => value % 2 == 0);
        await Assert.That(filtered.Keys.Order().SequenceEqual(new[] { 1, 3 })).IsTrue();
        filtered.CaptureChanges();

        filtered.FilterChanges(new ChangeSet<int, int>
        {
            new(ChangeReason.Refresh, 1, 2),
            new(ChangeReason.Refresh, 2, 6),
            new(ChangeReason.Refresh, 3, 5),
            new(ChangeReason.Refresh, 4, 7)
        }, value => value % 2 == 0);
        var refreshChanges = filtered.CaptureChanges();
        await Assert.That(refreshChanges.Refreshes).IsEqualTo(1);
        await Assert.That(refreshChanges.Adds).IsEqualTo(1);
        await Assert.That(refreshChanges.Removes).IsEqualTo(1);
        await Assert.That(filtered.Lookup(2).Value).IsEqualTo(6);

        filtered.FilterChanges(new ChangeSet<int, int>
        {
            new(ChangeReason.Update, 1, 8, ReactiveUI.Primitives.Optional<int>.Create(2)),
            new(ChangeReason.Update, 2, 9, ReactiveUI.Primitives.Optional<int>.Create(6)),
            new(ChangeReason.Remove, 1, 8)
        }, value => value % 2 == 0);
        await Assert.That(filtered.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RefilterRetainsMatchingItemsWithoutSpuriousUpdates()
    {
        var all = new Cache<int, int>();
        var filtered = new ChangeAwareCache<int, int>();
        await Assert.That(filtered.RefreshFilteredFrom(all, value => true)).IsEmpty();
        all.AddOrUpdate(2, 1);
        all.AddOrUpdate(3, 2);
        all.AddOrUpdate(4, 3);
        await Assert.That(filtered.RefreshFilteredFrom(all, value => value % 2 == 0).Adds).IsEqualTo(2);
        await Assert.That(filtered.RefreshFilteredFrom(all, value => value % 2 == 0)).IsEmpty();
        var changed = filtered.RefreshFilteredFrom(all, value => value >= 3);
        await Assert.That(changed.Adds).IsEqualTo(1);
        await Assert.That(changed.Removes).IsEqualTo(1);
        await Assert.That(filtered.Items.Order().SequenceEqual(new[] { 3, 4 })).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CacheListCombinationTracksMembershipAndDisposesConnections(bool readOnlyCaches)
    {
        using var first = new SourceCache<int, int>(value => value);
        using var second = new SourceCache<int, int>(value => value);
        first.AddOrUpdate(new[] { 1, 2 });
        second.AddOrUpdate(new[] { 2, 3 });
        using var mutable = new SourceList<ISourceCache<int, int>>();
        using var readOnly = new SourceList<IObservableCache<int, int>>();
        mutable.AddRange(new[] { first, second });
        readOnly.AddRange(new[] { first, second });
        using var intersection = (readOnlyCaches ? readOnly.And() : mutable.And()).AsAggregator();
        using var union = (readOnlyCaches ? readOnly.Or() : mutable.Or()).AsAggregator();
        using var except = (readOnlyCaches ? readOnly.Except() : mutable.Except()).AsAggregator();
        using var exclusive = (readOnlyCaches ? readOnly.Xor() : mutable.Xor()).AsAggregator();

        await Assert.That(intersection.Data.Items).IsEquivalentTo(new[] { 2 });
        await Assert.That(union.Data.Items).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(except.Data.Items).IsEquivalentTo(new[] { 1 });
        await Assert.That(exclusive.Data.Items).IsEquivalentTo(new[] { 1, 3 });
        second.RemoveKey(2);
        await Assert.That(intersection.Data.Items).IsEmpty();
        await Assert.That(exclusive.Data.Items).IsEquivalentTo(new[] { 1, 2, 3 });
        var messageCount = union.Messages.Count;
        union.Dispose();
        first.AddOrUpdate(4);
        await Assert.That(union.Messages.Count).IsEqualTo(messageCount);
    }
}
