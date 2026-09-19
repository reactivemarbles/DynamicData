using System.Collections.ObjectModel;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public class ListLifecycleCoverageFixture
{
    [Test]
    public async Task SourceListCountChangedAfterDisposePublishesFinalCountAndCompletes()
    {
        var source = new SourceList<int>();
        source.AddRange(new[] { 1, 2, 3 });
        source.Dispose();

        using var subscription = source.CountChanged.RecordValues(out var observer);

        await Assert.That(observer.RecordedValues).IsEquivalentTo(new[] { 3 });
        await Assert.That(observer.HasCompleted).IsTrue();
    }

    [Test]
    public async Task SourceListConnectDuringEditWaitsForCompletedEditSnapshot()
    {
        using var source = new SourceList<int>();
        IDisposable? connection = null;
        ListItemRecordingObserver<int>? observer = null;

        source.Edit(items =>
        {
            items.Add(1);
            connection = source.Connect().RecordListItems(out observer);
            items.Add(2);
        });

        using (connection!)
        {
            await Assert.That(observer!.RecordedChangeSets.Count).IsEqualTo(1);
            await Assert.That(observer.RecordedChangeSets[0].Adds).IsEqualTo(2);
            await Assert.That(observer.RecordedItems).IsEquivalentTo(new[] { 1, 2 });
        }
    }

    [Test]
    public async Task SourceListPreviewAfterDisposeCompletesWithoutChanges()
    {
        var source = new SourceList<int>();
        source.Add(1);
        source.Dispose();

        using var subscription = source.Preview().RecordListItems(out var observer);

        await Assert.That(observer.RecordedChangeSets).IsEmpty();
        await Assert.That(observer.HasCompleted).IsTrue();
    }

    [Test]
    public async Task SourceListSourceErrorsReachConnectPreviewAndCountObservers()
    {
        using var upstream = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var source = new SourceList<int>(upstream);

        source.Edit(items =>
        {
            using var pending = source.Connect().Subscribe(_ => { });
            items.Add(1);
        });

        using var connectSubscription = source.Connect().RecordListItems(out var connect);
        using var previewSubscription = source.Preview().RecordListItems(out var preview);
        using var countSubscription = source.CountChanged.RecordValues(out var counts);
        var error = new InvalidOperationException("expected");

        upstream.OnError(error);

        await Assert.That(connect.Error).IsSameReferenceAs(error);
        await Assert.That(preview.Error).IsSameReferenceAs(error);
        await Assert.That(counts.Error).IsSameReferenceAs(error);
    }

    [Test]
    public async Task SourceListSourceCompletionReachesConnectAndCountObservers()
    {
        using var upstream = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<int>>();
        using var source = new SourceList<int>(upstream);

        source.Edit(items =>
        {
            using var pending = source.Connect().Subscribe(_ => { });
            items.Add(1);
        });

        using var connectSubscription = source.Connect().RecordListItems(out var connect);
        using var countSubscription = source.CountChanged.RecordValues(out var counts);

        upstream.OnCompleted();

        await Assert.That(connect.HasCompleted).IsTrue();
        await Assert.That(counts.HasCompleted).IsTrue();
    }

    [Test]
    public async Task ChangeAwareListCopyConstructorCanPreservePendingChanges()
    {
        var original = new ChangeAwareList<int>();
        original.Add(42);

        var copy = new ChangeAwareList<int>(original, copyChanges: true);
        var copiedChanges = copy.CaptureChanges();
        var originalChanges = original.CaptureChanges();

        await Assert.That(copy.IsReadOnly).IsFalse();
        await Assert.That(copy.Contains(42)).IsTrue();
        await Assert.That(copiedChanges.Count).IsEqualTo(1);
        await Assert.That(copiedChanges.Adds).IsEqualTo(1);
        await Assert.That(originalChanges.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ChangeAwareListBoundaryOperationsThrowBeforeMutation()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2 });
        list.CaptureChanges();

        await Assert.That(() => list.Insert(-1, 0)).Throws<ArgumentException>();
        await Assert.That(() => list.Insert(3, 0)).Throws<ArgumentException>();
        await Assert.That(() => list.Move(1, -1)).Throws<ArgumentException>();
        await Assert.That(() => list.Move(3, 0)).Throws<ArgumentException>();
        await Assert.That(list.Refresh(9)).IsFalse();
        await Assert.That(() => list.RefreshAt(-1)).Throws<ArgumentException>();
        await Assert.That(() => list.RemoveAt(-1)).Throws<ArgumentException>();
        await Assert.That(() => list.RemoveRange(2, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(list.CaptureChanges()).IsEmpty();
    }

    [Test]
    public async Task ChangeAwareListInsertBatchingExtendsRangesAtStartAndEnd()
    {
        var list = new ChangeAwareList<int>();

        list.InsertRange(new[] { 2, 3 }, 0);
        list.Insert(0, 1);
        list.Insert(3, 4);

        var changes = list.CaptureChanges();
        var change = changes.Single();

        await Assert.That(change.Reason).IsEqualTo(ListChangeReason.AddRange);
        await Assert.That(change.Range.Index).IsEqualTo(0);
        await Assert.That(change.Range).IsEquivalentTo(new[] { 1, 2, 3, 4 });
    }

    [Test]
    public async Task ChangeAwareListRemoveBatchingExtendsRangesInReverseOrder()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3, 4 });
        list.CaptureChanges();

        list.RemoveAt(2);
        list.RemoveAt(2);
        list.RemoveAt(1);

        var changes = list.CaptureChanges();
        var change = changes.Single();

        await Assert.That(change.Reason).IsEqualTo(ListChangeReason.RemoveRange);
        await Assert.That(change.Range.Index).IsEqualTo(1);
        await Assert.That(change.Range).IsEquivalentTo(new[] { 2, 3, 4 });
    }

    [Test]
    public async Task ChangeAwareListRemoveRangeWithZeroCountDoesNotRecordChange()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3 });
        list.CaptureChanges();

        list.RemoveRange(1, 0);

        await Assert.That(list).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(list.CaptureChanges()).IsEmpty();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RefCountChangeAwareListTracksDuplicatesThroughRangeSetAndClear(bool useAddRange)
    {
        var list = new ChangeAwareListWithRefCounts<string>();

        if (useAddRange)
        {
            list.AddRange(new[] { "a", "a", "b" });
        }
        else
        {
            list.InsertRange(new[] { "a", "a", "b" }, 0);
        }

        list.Remove("a");
        await Assert.That(list.Contains("a")).IsTrue();

        list[0] = "c";
        await Assert.That(list.Contains("a")).IsFalse();
        await Assert.That(list.Contains("c")).IsTrue();

        list.RemoveRange(0, 1);
        await Assert.That(list.Contains("c")).IsFalse();

        list.Clear();
        await Assert.That(list.Contains("b")).IsFalse();
        await Assert.That(list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ListExFallbackRangeAndSearchOperationsUsePlainIListSemantics()
    {
        IList<int> plain = new Collection<int>();
        plain.AddRange(new[] { 2, 3 });
        plain.AddOrInsertRange(new[] { 0, 1 }, 0);
        plain.AddOrInsertRange(new[] { 4, 5 }, -1);
        var missing = plain.IndexOf(99, EqualityComparer<int>.Default);
        var found = plain.IndexOfOptional(3);

        await Assert.That(plain).IsEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
        await Assert.That(missing).IsEqualTo(-1);
        await Assert.That(found.HasValue).IsTrue();
        await Assert.That(found.Value.Index).IsEqualTo(3);
        await Assert.That(new[] { 1, 3, 5 }.BinarySearch(3)).IsEqualTo(1);
    }

    [Test]
    public async Task ListExReplaceVariantsCoverMissingAndComparerPaths()
    {
        IList<string> values = new List<string> { "aa" };
        var comparer = new SameLengthComparer();

        values.Replace("aa", "bb", comparer);
        values.ReplaceOrAdd("missing", "cc");

        await Assert.That(values).IsEquivalentTo(new[] { "bb", "cc" });
        await Assert.That(() => values.Replace("missing", "dd")).Throws<ArgumentException>();
    }

    [Test]
    public async Task ListExCloneAppliesNonExtendedListRefreshMoveAndReplaceShapes()
    {
        IList<int> target = new Collection<int> { 1, 2, 3 };

        target.Clone(new[]
        {
            new Change<int>(ListChangeReason.Refresh, 20, 1),
            new Change<int>(20, 2, 1),
            new Change<int>(ListChangeReason.Replace, 40, ReactiveUI.Primitives.Optional<int>.Create(20), -1, 2),
            new Change<int>(ListChangeReason.Replace, 50, ReactiveUI.Primitives.Optional<int>.Create(40), 0, -1)
        }, null);

        await Assert.That(target).IsEquivalentTo(new[] { 50, 1, 3 });
        await Assert.That(new Change<int>(ListChangeReason.Add, 1).MovedWithinRange(0, 1)).IsFalse();
        await Assert.That(new Change<int>(1, 2, 0).MovedWithinRange(1, 1)).IsFalse();
        await Assert.That(() => target.Clone(new[] { new Change<int>(ListChangeReason.Moved, 1, ReactiveUI.Primitives.Optional<int>.None, -1, 0) }, null)).Throws<UnspecifiedIndexException>();
    }

    private sealed class SameLengthComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x?.Length == y?.Length;

        public int GetHashCode(string obj) => obj.Length;
    }
}
