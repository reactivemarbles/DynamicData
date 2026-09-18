namespace DynamicData.Tests.List;

public class OnItemAddedFixture
{
    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 0)]
    [Arguments(1, 1)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 5)]
    public async Task ItemIsAdded_AddActionIsInvoked(
        int initialItemCount,
        int insertionIndex)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        if (initialItemCount is 0)
            await Assert.That(results.RecordedChangeSets).IsEmpty().Because("there were no initial items to be published");
        else
            await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        if (initialItemCount is 0)
            await Assert.That(addActionInvocations).IsEmpty().Because("no initial items were added to the collection");
        else
            await Assert.That(addActionInvocations).IsEquivalentTo(
                source.Items,
                TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.Insert(
            index: insertionIndex,
            item: initialItemCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was added to the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEquivalentTo(new[] { initialItemCount }).Because("an item was added to the collection");
    }

    [Test]
    [Arguments(2, 0, 1)]
    [Arguments(2, 1, 0)]
    [Arguments(5, 0, 4)]
    [Arguments(5, 4, 0)]
    [Arguments(5, 1, 3)]
    [Arguments(5, 3, 1)]
    public async Task ItemIsMoved_AddActionIsNotInvoked(
        int initialItemCount,
        int originalIndex,
        int destinationIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.Move(
            original: originalIndex,
            destination: destinationIndex);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was moved within the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEmpty().Because("no items were added to the collection");
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 4)]
    public async Task ItemIsRefreshed_AddActionIsNotInvoked(
        int initialItemCount,
        int refreshIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.Refresh(refreshIndex);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was refreshed within the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEmpty().Because("no items were added to the collection");
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 4)]
    public async Task ItemIsRemoved_AddActionIsNotInvoked(
        int initialItemCount,
        int removalIndex)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.RemoveAt(removalIndex);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was removed from the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEmpty().Because("no items were added to the collection");
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(5, 0)]
    [Arguments(5, 2)]
    [Arguments(5, 4)]
    public async Task ItemIsReplaced_AddActionIsInvokedForNewItem(
        int initialItemCount,
        int replacementIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.ReplaceAt(
            index: replacementIndex,
            item: initialItemCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("an item was replaced within the collection");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEquivalentTo(new[] { initialItemCount }).Because("an item was replaced within the collection");
    }

    [Test]
    [Arguments(1, 0, 1)]
    [Arguments(5, 0, 1)]
    [Arguments(5, 2, 1)]
    [Arguments(5, 1, 3)]
    [Arguments(5, 0, 5)]
    public async Task ItemRangeIsRemoved_AddActionIsNotInvoked(
        int initialItemCount,
        int removalIndex,
        int removalCount)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.RemoveRange(
            index: removalIndex,
            count: removalCount);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEmpty().Because("no items were added to the collection");
    }

    [Test]
    [Arguments(1)]
    [Arguments(5)]
    public async Task ItemsAreCleared_AddActionIsNotInvoked(int initialItemCount)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.Clear();

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("all items in the collection should have been removed");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEmpty().Because("no items were added to the collection");
    }

    [Test]
    public async Task SourceCompletesAsynchronously_CompletionPropagates()
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        source.Complete();

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed");
        await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no changes were made to the collection");

        await Assert.That(addActionInvocations).IsEmpty().Because("no items were added to the collection");
    }

    [Test]
    public async Task SourceCompletesImmediately_CompletionPropagates()
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });
        source.Complete();

        var addActionInvocations = new List<int>();

        // UUT Construction & Action
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsTrue().Because("the source has completed");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();
    }

    [Test]
    public async Task SourceFailsAsynchronously_CompletionPropagates()
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });

        var addActionInvocations = new List<int>();

        // UUT Construction
        using var subscription = source.Connect()
            .OnItemAdded(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsNull().Because("no errors should have occurred");
        await Assert.That(results.HasCompleted).IsFalse().Because("the source can still publish notifications");
        await Assert.That(results.RecordedChangeSets).HasSingleItem().Because("the initial items should have been published");
        await Assert.That(results.RecordedItems).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Matching).Because("all collection changes should propagate downstream");
        results.ClearChangeSets();

        await Assert.That(addActionInvocations).IsEquivalentTo(
            source.Items,
            TUnit.Assertions.Enums.CollectionOrdering.Any).Because("the collection contained initial items");
        addActionInvocations.Clear();

        // UUT Action
        var error = new Exception();
        source.SetError(error);

        await Assert.That(results.Error).IsSameReferenceAs(error).Because("errors within the stream should propagate");
        await Assert.That(results.RecordedChangeSets).IsEmpty().Because("no changes were made to the collection");

        await Assert.That(addActionInvocations).IsEmpty().Because("no items were added to the collection");
    }

    [Test]
    public async Task SourceFailsImmediately_CompletionPropagates()
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

        var addActionInvocations = new List<int>();

        // UUT Construction & Action
        using var subscription = source.Connect()
            .OnItemRemoved(addActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);

        await Assert.That(results.Error).IsSameReferenceAs(error).Because("errors within the stream should propagate");
        await Assert.That(results.RecordedChangeSets).IsEmpty().Because("an error occurred during subscription");

        await Assert.That(addActionInvocations).IsEmpty().Because("an error occurred during subscription");
    }
}
