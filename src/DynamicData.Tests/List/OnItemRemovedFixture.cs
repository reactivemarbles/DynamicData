namespace DynamicData.Tests.List;

public class OnItemRemovedFixture
{
    // https://github.com/reactivemarbles/DynamicData/issues/1061
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task SubscriberDoesNotHandleErrors_ErrorBubblesUpstream(bool invokeOnUnsubscribe)
    {
        using var source = new TestSourceList<int>();

        using var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: static _ => { },
                invokeOnUnsubscribe: invokeOnUnsubscribe)
            .Subscribe();

        var error = new Exception("Test");

        var exception = await Assert.That(() => source.SetError(error))
            .Throws<Exception>().Because("errors not handled by the subscriber should propagate upstream to the caller");

        await Assert.That(exception).IsSameReferenceAs(error);
    }

    [Test]
    [Arguments(0, 0, 0)]
    [Arguments(1, 0, 0)]
    [Arguments(1, 0, 1)]
    [Arguments(5, 0, 1)]
    [Arguments(5, 2, 1)]
    [Arguments(5, 1, 3)]
    [Arguments(5, 0, 5)]
    public async Task InvokeOnUnsubscribeIsRequested_RemoveActionIsInvokedForEachRemainingItemOnCompletion(
        int initialItemCount,
        int removalIndex,
        int removalCount)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: removeActionInvocations.Add,
                invokeOnUnsubscribe: true)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        if (initialItemCount is 0)
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("there were no initial items to be published");
        else
            await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Setup: Remove some items, to ensure correct tracking of remaining items.
        var removedItems = source.Items
            .Skip(removalIndex)
            .Take(removalCount)
            .ToArray();

        if (removalCount is not 0)
            source.RemoveRange(
                index: removalIndex,
                count: removalCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        if (removalCount is 0)
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no items should have been removed");
        else
            await Assert.That(results.RecordedChangeSets).HasSingleItem().Because($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).IsEquivalentTo(removedItems, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the removal action should be invoked for every removed item");
        removeActionInvocations.Clear();

        // UUT Action
        subscription.Dispose();

        await Assert.That(removeActionInvocations).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the removal action should be invoked for all all remaining items");
    }

    [Test]
    [Arguments(0, 0, 0)]
    [Arguments(1, 0, 0)]
    [Arguments(1, 0, 1)]
    [Arguments(5, 0, 1)]
    [Arguments(5, 2, 1)]
    [Arguments(5, 1, 3)]
    [Arguments(5, 0, 5)]
    public async Task InvokeOnUnsubscribeIsNotRequested_RemoveActionIsNotInvokedOnCompletion(
        int initialItemCount,
        int removalIndex,
        int removalCount)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: removeActionInvocations.Add,
                invokeOnUnsubscribe: false)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        if (initialItemCount is 0)
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("there were no initial items to be published");
        else
            await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Setup: Remove some items, to ensure correct tracking of remaining items.
        var removedItems = source.Items
            .Skip(removalIndex)
            .Take(removalCount)
            .ToArray();

        if (removalCount is not 0)
            source.RemoveRange(
                index: removalIndex,
                count: removalCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        if (removalCount is 0)
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no items should have been removed");
        else
            await Assert.That(results.RecordedChangeSets).HasSingleItem().Because($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).IsEquivalentTo(removedItems, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the removal action should be invoked for every removed item");
        removeActionInvocations.Clear();

        // UUT Action
        subscription.Dispose();

        await Assert.That(removeActionInvocations).IsEmpty().Because("the removal action should not be invoked upon unsubscription");
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 0)]
    [Arguments(1, 1)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 5)]
    public async Task ItemIsAdded_RemoveActionIsNotInvoked(
        int initialItemCount,
        int insertionIndex)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(removeActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        if (initialItemCount is 0)
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("there were no initial items to be published");
        else
            await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        source.Insert(
            index: insertionIndex,
            item: initialItemCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was refreshed within the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items were removed from the collection");
    }

    [Test]
    [Arguments(2, 0, 1)]
    [Arguments(2, 1, 0)]
    [Arguments(5, 0, 4)]
    [Arguments(5, 4, 0)]
    [Arguments(5, 1, 3)]
    [Arguments(5, 3, 1)]
    public async Task ItemIsMoved_RemoveActionIsNotInvoked(
        int initialItemCount,
        int originalIndex,
        int destinationIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(removeActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        source.Move(
            original: originalIndex,
            destination: destinationIndex);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was moved within the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items were removed from the collection");
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 4)]
    public async Task ItemIsRemoved_RemoveActionIsInvoked(
        int initialItemCount,
        int removalIndex)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(removeActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        var removedItem = source.Items[removalIndex];
        source.RemoveAt(removalIndex);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was removed from the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).HasSingleItem().Because("an item was removed from the collection");
        await Assert.That(removeActionInvocations.ElementAt(0)).IsEqualTo(removedItem).Because("an item was removed from the collection");
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 4)]
    public async Task ItemIsRefreshed_RemoveActionIsNotInvoked(
        int initialItemCount,
        int refreshIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(removeActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        source.Refresh(refreshIndex);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was refreshed within the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items were removed from the collection");
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 4)]
    public async Task ItemIsReplaced_RemoveActionIsInvokedForOldItem(
        int initialItemCount,
        int replacementIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(removeActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        var replacedItem = source.Items[replacementIndex];
        source.ReplaceAt(
            index: replacementIndex,
            item: initialItemCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was replaced within the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).HasSingleItem().Because("an item was replaced within the collection");
        await Assert.That(removeActionInvocations.ElementAt(0)).IsEqualTo(replacedItem).Because("an item was replaced within the collection");
    }

    [Test]
    [Arguments(1, 0, 1)]
    [Arguments(5, 0, 1)]
    [Arguments(5, 2, 1)]
    [Arguments(5, 1, 3)]
    [Arguments(5, 0, 5)]
    public async Task ItemRangeIsRemoved_RemoveActionIsInvokedForEachItem(
        int initialItemCount,
        int removalIndex,
        int removalCount)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: removeActionInvocations.Add,
                invokeOnUnsubscribe: true)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        var removedItems = source.Items
            .Skip(removalIndex)
            .Take(removalCount)
            .ToArray();

        source.RemoveRange(
            index: removalIndex,
            count: removalCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).IsEquivalentTo(removedItems, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the removal action should be invoked for every removed item");
    }

    [Test]
    [Arguments(1)]
    [Arguments(5)]
    public async Task ItemsAreCleared_RemoveActionIsInvokedForEachItem(int initialItemCount)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(removeActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        var clearedItems = source.Items
            .ToArray();

        source.Clear();

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("all items in the collection should have been removed");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(removeActionInvocations).IsEquivalentTo(clearedItems, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the removal action should be invoked for every removed item");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task SourceCompletesAsynchronously_CompletionPropagates(bool invokeOnUnsubscribe)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: removeActionInvocations.Add,
                invokeOnUnsubscribe: invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        source.Complete();

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed");
        await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no changes were made to the collection");

        if (invokeOnUnsubscribe)
            await Assert.That(removeActionInvocations).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the operator was instructed to invoke the removal action all remaining items, upon stream completion");
        else
            await Assert.That(removeActionInvocations).IsEmpty().Because("the operator was instructed to not invoke the removal action, upon stream completion");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task SourceCompletesImmediately_CompletionPropagates(bool invokeOnUnsubscribe)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });
        source.Complete();

        var removeActionInvocations = new List<int>();

        // UUT Construction & Action
        using var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: removeActionInvocations.Add,
                invokeOnUnsubscribe: invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        if (invokeOnUnsubscribe)
            await Assert.That(removeActionInvocations).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the operator was instructed to invoke the removal action all remaining items, upon stream completion");
        else
            await Assert.That(removeActionInvocations).IsEmpty().Because("the operator was instructed to not invoke the removal action, upon stream completion");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task SourceFailsAsynchronously_CompletionPropagates(bool invokeOnUnsubscribe)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });

        var removeActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: removeActionInvocations.Add,
                invokeOnUnsubscribe: invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(removeActionInvocations).IsEmpty().Because("no items have been removed from the collection");

        // UUT Action
        var error = new Exception();
        source.SetError(error);

        await Assert.That(results.Error).IsSameReferenceAs(error).Because("errors within the stream should propagate");
        await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no changes were made to the collection");

        if (invokeOnUnsubscribe)
            await Assert.That(removeActionInvocations).IsEquivalentTo(source.Items, TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the operator was instructed to invoke the removal action all remaining items, upon stream failure");
        else
            await Assert.That(removeActionInvocations).IsEmpty().Because("the operator was instructed to not invoke the removal action, upon stream failure");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task SourceFailsImmediately_CompletionPropagates(bool invokeOnUnsubscribe)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });
        var error = new Exception();
        source.SetError(error);

        var removeActionInvocations = new List<int>();

        // UUT Construction & Action
        using var subscription = source.Connect()
            .OnItemRemoved(
                removeAction: removeActionInvocations.Add,
                invokeOnUnsubscribe: invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsSameReferenceAs(error).Because("errors within the stream should propagate");
        await Assert.That(results.RecordedChangeSets).IsEmpty().Because("an error occurred during subscription");

        await Assert.That(removeActionInvocations).IsEmpty().Because(invokeOnUnsubscribe
            ? "the initial items in the collection were never published"
            : "the operator was instructed to not invoke the removal action, upon stream failure");
    }
}
