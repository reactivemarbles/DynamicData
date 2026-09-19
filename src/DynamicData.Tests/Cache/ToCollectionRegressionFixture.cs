namespace DynamicData.Tests.Cache;

public class ToCollectionRegressionFixture
{
    [Test]
    public async Task SubscriptionsHaveIndependentStateAndSnapshotsRemainUnchanged()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int, int>>();
        var snapshots = source.ToCollection();
        var first = new List<IReadOnlyCollection<int>>();
        var second = new List<IReadOnlyCollection<int>>();
        using var firstSubscription = snapshots.Subscribe(first.Add);
        var changes = new ChangeAwareCache<int, int>();

        changes.AddOrUpdate(10, 1);
        source.OnNext(changes.CaptureChanges());
        using var secondSubscription = snapshots.Subscribe(second.Add);
        changes.AddOrUpdate(20, 2);
        source.OnNext(changes.CaptureChanges());
        changes.Remove(1);
        source.OnNext(changes.CaptureChanges());

        await Assert.That(first.Count).IsEqualTo(3);
        await Assert.That(first[0]).IsEquivalentTo(new[] { 10 });
        await Assert.That(first[1]).IsEquivalentTo(new[] { 10, 20 });
        await Assert.That(first[2]).IsEquivalentTo(new[] { 20 });
        await Assert.That(second.Count).IsEqualTo(2);
        await Assert.That(second[0]).IsEquivalentTo(new[] { 20 });
        await Assert.That(second[1]).IsEquivalentTo(new[] { 20 });
    }

    [Test]
    public async Task DisposingOneSubscriptionLeavesTheOtherActive()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int, int>>();
        var snapshots = source.ToCollection();
        var first = new List<IReadOnlyCollection<int>>();
        var second = new List<IReadOnlyCollection<int>>();
        using var firstSubscription = snapshots.Subscribe(first.Add);
        using var secondSubscription = snapshots.Subscribe(second.Add);
        firstSubscription.Dispose();

        var changes = new ChangeAwareCache<int, int>();
        changes.AddOrUpdate(10, 1);
        source.OnNext(changes.CaptureChanges());

        await Assert.That(first).IsEmpty();
        await Assert.That(second.Count).IsEqualTo(1);
        await Assert.That(second[0]).IsEquivalentTo(new[] { 10 });
        await Assert.That(source.HasObservers).IsTrue();
        secondSubscription.Dispose();
        await Assert.That(source.HasObservers).IsFalse();
    }

    [Test]
    public async Task EmptyChangesAndRefreshesProduceSnapshotsWithoutMutatingEarlierResults()
    {
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int, int>>();
        var snapshots = new List<IReadOnlyCollection<int>>();
        using var subscription = source.ToCollection().Subscribe(snapshots.Add);
        var changes = new ChangeAwareCache<int, int>();
        source.OnNext(changes.CaptureChanges());
        changes.AddOrUpdate(10, 1);
        source.OnNext(changes.CaptureChanges());
        changes.Refresh(1);
        source.OnNext(changes.CaptureChanges());
        changes.AddOrUpdate(20, 1);
        source.OnNext(changes.CaptureChanges());

        await Assert.That(snapshots.Count).IsEqualTo(4);
        await Assert.That(snapshots[0]).IsEmpty();
        await Assert.That(snapshots[1]).IsEquivalentTo(new[] { 10 });
        await Assert.That(snapshots[2]).IsEquivalentTo(new[] { 10 });
        await Assert.That(snapshots[3]).IsEquivalentTo(new[] { 20 });
    }
}
