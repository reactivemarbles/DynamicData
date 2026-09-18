namespace DynamicData.Tests.List;

public class ListValueCoverageFixture
{
    [Test]
    public async Task ChangeConstructorsValidateReasonShapeAndIndexes()
    {
        await Assert.That(() => new Change<int>(ListChangeReason.Add, new[] { 1 })).Throws<IndexOutOfRangeException>();
        await Assert.That(() => new Change<int>(1, -1, 0)).Throws<ArgumentException>();
        await Assert.That(() => new Change<int>(1, 0, -1)).Throws<ArgumentException>();
        await Assert.That(() => new Change<int>(ListChangeReason.Add, 1, new ReactiveUI.Primitives.Optional<int>(0))).Throws<ArgumentException>();
        await Assert.That(() => new Change<int>(ListChangeReason.Replace, 1)).Throws<ArgumentException>();
        await Assert.That(() => new Change<int>(ListChangeReason.Refresh, 1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task ChangeEqualityHashCodeAndFormattingUseValueState()
    {
        var first = new Change<int>(ListChangeReason.Replace, 2, new ReactiveUI.Primitives.Optional<int>(1), 0, 0);
        var same = new Change<int>(ListChangeReason.Replace, 2, new ReactiveUI.Primitives.Optional<int>(1), 0, 0);
        var different = new Change<int>(ListChangeReason.Add, 2, 0);

        await Assert.That(first == same).IsTrue();
        await Assert.That(first != different).IsTrue();
        await Assert.That(first.Equals((object)same)).IsTrue();
        await Assert.That(first.Equals((object?)null)).IsFalse();
        await Assert.That(first.Equals("not a change")).IsFalse();
        await Assert.That(first.GetHashCode()).IsEqualTo(same.GetHashCode());
        await Assert.That(new Change<int>(ListChangeReason.AddRange, new[] { 1, 2 }).ToString()).IsEqualTo("AddRange. 2 changes");
    }

    [Test]
    public async Task ChangeAwareListConstructorsCopyItemsAndChanges()
    {
        var original = new ChangeAwareList<int>(capacity: 4);
        original.AddRange(new[] { 1, 2 });

        var copiedWithChanges = new ChangeAwareList<int>(original, copyChanges: true);
        var copiedWithoutChanges = new ChangeAwareList<int>(original, copyChanges: false);

        await Assert.That(original.Capacity).IsGreaterThanOrEqualTo(4);
        await Assert.That(original.IsReadOnly).IsFalse();
        await Assert.That(original.Contains(1)).IsTrue();
        await Assert.That(copiedWithChanges.CaptureChanges().Adds).IsEqualTo(2);
        await Assert.That(copiedWithoutChanges.CaptureChanges()).IsEmpty();
    }

    [Test]
    public async Task ChangeAwareListBatchesAdjacentAddsAndRemoves()
    {
        var addedAtSameIndex = new ChangeAwareList<int>();
        addedAtSameIndex.Insert(0, 2);
        addedAtSameIndex.Insert(0, 1);

        var sameIndexChanges = addedAtSameIndex.CaptureChanges();
        await Assert.That(sameIndexChanges.Single().Reason).IsEqualTo(ListChangeReason.AddRange);
        await Assert.That(sameIndexChanges.Single().Range).IsEquivalentTo(new[] { 1, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var addedAtEnd = new ChangeAwareList<int>();
        addedAtEnd.Add(1);
        addedAtEnd.Add(2);

        var endChanges = addedAtEnd.CaptureChanges();
        await Assert.That(endChanges.Single().Reason).IsEqualTo(ListChangeReason.AddRange);
        await Assert.That(endChanges.Single().Range).IsEquivalentTo(new[] { 1, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var removedForward = new ChangeAwareList<int>(new[] { 1, 2, 3, 4 });
        removedForward.CaptureChanges();
        removedForward.RemoveAt(1);
        removedForward.RemoveAt(1);

        var forwardChanges = removedForward.CaptureChanges();
        await Assert.That(forwardChanges.Single().Reason).IsEqualTo(ListChangeReason.RemoveRange);
        await Assert.That(forwardChanges.Single().Range).IsEquivalentTo(new[] { 2, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var removedBackward = new ChangeAwareList<int>(new[] { 1, 2, 3, 4 });
        removedBackward.CaptureChanges();
        removedBackward.RemoveAt(2);
        removedBackward.RemoveAt(1);

        var backwardChanges = removedBackward.CaptureChanges();
        await Assert.That(backwardChanges.Single().Reason).IsEqualTo(ListChangeReason.RemoveRange);
        await Assert.That(backwardChanges.Single().Range).IsEquivalentTo(new[] { 2, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ChangeAwareListHandlesNoOpsAndGuardClauses()
    {
        var list = new ChangeAwareList<int>(new[] { 1, 2, 3 });
        list.CaptureChanges();

        await Assert.That(list.Refresh(9)).IsFalse();

        list.AddRange(Array.Empty<int>());
        list.InsertRange(Array.Empty<int>(), 1);
        list.RemoveRange(1, 0);

        await Assert.That(list.CaptureChanges()).IsEmpty();
        await Assert.That(() => list.Insert(-1, 0)).Throws<ArgumentException>();
        await Assert.That(() => list.Insert(5, 0)).Throws<ArgumentException>();
        await Assert.That(() => list.Move(-1, 0)).Throws<ArgumentException>();
        await Assert.That(() => list.Move(0, -1)).Throws<ArgumentException>();
        await Assert.That(() => list.Refresh(1, -1)).Throws<ArgumentException>();
        await Assert.That(() => list.RefreshAt(-1)).Throws<ArgumentException>();
        await Assert.That(() => list.RemoveAt(-1)).Throws<ArgumentException>();
        await Assert.That(() => list[5] = 5).Throws<ArgumentException>();
    }

    [Test]
    public async Task ListExUsesFallbackListOperations()
    {
        var collection = new Collection<int> { 3 };

        collection.AddOrInsertRange(new[] { 1, 2 }, 0);
        collection.AddRange(new[] { 4, 5 });
        collection.AddRange(new[] { 6, 7 }, 0);
        collection.Remove(new[] { 6, 7 });

        await Assert.That(collection).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(collection.IndexOf(99, EqualityComparer<int>.Default)).IsEqualTo(-1);

        collection.ReplaceOrAdd(42, 6);
        collection.Replace(6, 7);

        await Assert.That(collection).IsEquivalentTo(new[] { 1, 2, 3, 4, 5, 7 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(() => collection.Replace(42, 8)).Throws<ArgumentException>();
    }

    [Test]
    public async Task ListExClonesIndexedReplaceMoveAndRefreshChanges()
    {
        var source = new ChangeAwareList<int>(new[] { 1, 2, 3 });
        source.CaptureChanges();
        source[1] = 20;
        source.Move(2, 0);
        source.RefreshAt(0);

        var target = new ChangeAwareList<int>(new[] { 1, 2, 3 });
        target.CaptureChanges();
        target.Clone(source.CaptureChanges());

        await Assert.That(target).IsEquivalentTo(new[] { 3, 1, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var changes = target.CaptureChanges().ToList();
        await Assert.That(changes.Select(static change => change.Reason)).IsEquivalentTo(
            new[] { ListChangeReason.Replace, ListChangeReason.Moved, ListChangeReason.Refresh },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
