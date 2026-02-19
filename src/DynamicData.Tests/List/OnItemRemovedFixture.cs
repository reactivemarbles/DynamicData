using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public class OnItemRemovedFixture
{
    [Theory]
    [InlineData(0,  0,  0)]
    [InlineData(1,  0,  0)]
    [InlineData(1,  0,  1)]
    [InlineData(5,  0,  1)]
    [InlineData(5,  2,  1)]
    [InlineData(5,  1,  3)]
    [InlineData(5,  0,  5)]
    public void InvokeOnUnsubscribeIsRequested_RemoveActionIsInvokedForEachRemainingItemOnCompletion(
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
                removeAction:           removeActionInvocations.Add,
                invokeOnUnsubscribe:    true)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        if (initialItemCount is 0)
            results.RecordedChangeSets.Should().BeEmpty("there were no initial items to be published");
        else
            results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Setup: Remove some items, to ensure correct tracking of remaining items.
        var removedItems = source.Items
            .Skip(removalIndex)
            .Take(removalCount)
            .ToArray();

        if (removalCount is not 0)
            source.RemoveRange(
                index: removalIndex,
                count: removalCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        if (removalCount is 0)
            results.RecordedChangeSets.Should().BeEmpty("no items should have been removed");
        else
            results.RecordedChangeSets.Should().ContainSingle($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        
        removeActionInvocations.Should().BeEquivalentTo(removedItems, options => options.WithoutStrictOrdering(), "the removal action should be invoked for every removed item");
        removeActionInvocations.Clear();
        
        // UUT Action
        subscription.Dispose();
        
        removeActionInvocations.Should().BeEquivalentTo(source.Items, options => options.WithoutStrictOrdering(), "the removal action should be invoked for all all remaining items");
    }

    [Theory]
    [InlineData(0,  0,  0)]
    [InlineData(1,  0,  0)]
    [InlineData(1,  0,  1)]
    [InlineData(5,  0,  1)]
    [InlineData(5,  2,  1)]
    [InlineData(5,  1,  3)]
    [InlineData(5,  0,  5)]
    public void InvokeOnUnsubscribeIsNotRequested_RemoveActionIsNotInvokedOnCompletion(
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
                removeAction:           removeActionInvocations.Add,
                invokeOnUnsubscribe:    false)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        if (initialItemCount is 0)
            results.RecordedChangeSets.Should().BeEmpty("there were no initial items to be published");
        else
            results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Setup: Remove some items, to ensure correct tracking of remaining items.
        var removedItems = source.Items
            .Skip(removalIndex)
            .Take(removalCount)
            .ToArray();

        if (removalCount is not 0)
            source.RemoveRange(
                index: removalIndex,
                count: removalCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        if (removalCount is 0)
            results.RecordedChangeSets.Should().BeEmpty("no items should have been removed");
        else
            results.RecordedChangeSets.Should().ContainSingle($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        
        removeActionInvocations.Should().BeEquivalentTo(removedItems, options => options.WithoutStrictOrdering(), "the removal action should be invoked for every removed item");
        removeActionInvocations.Clear();
        
        // UUT Action
        subscription.Dispose();
        
        removeActionInvocations.Should().BeEmpty("the removal action should not be invoked upon unsubscription");
    }

    [Theory]
    [InlineData(0,  0)]
    [InlineData(1,  0)]
    [InlineData(1,  1)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  5)]
    public void ItemIsAdded_RemoveActionIsNotInvoked(
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
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        if (initialItemCount is 0)
            results.RecordedChangeSets.Should().BeEmpty("there were no initial items to be published");
        else
            results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        source.Insert(
            index:  insertionIndex,
            item:   initialItemCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was refreshed within the collection");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");

        removeActionInvocations.Should().BeEmpty("no items were removed from the collection");
    }

    [Theory]
    [InlineData(2,  0,  1)]
    [InlineData(2,  1,  0)]
    [InlineData(5,  0,  4)]
    [InlineData(5,  4,  0)]
    [InlineData(5,  1,  3)]
    [InlineData(5,  3,  1)]
    public void ItemIsMoved_RemoveActionIsNotInvoked(
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
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        source.Move(
            original:       originalIndex,
            destination:    destinationIndex);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was moved within the collection");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");

        removeActionInvocations.Should().BeEmpty("no items were removed from the collection");
    }

    [Theory]
    [InlineData(1,  0)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  4)]
    public void ItemIsRemoved_RemoveActionIsInvoked(
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
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        var removedItem = source.Items[removalIndex];
        source.RemoveAt(removalIndex);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was removed from the collection");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");

        removeActionInvocations.Should().ContainSingle("an item was removed from the collection");
        removeActionInvocations.ElementAt(0).Should().Be(removedItem, "an item was removed from the collection");
    }

    [Theory]
    [InlineData(1,  0)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  4)]
    public void ItemIsRefreshed_RemoveActionIsNotInvoked(
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
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        source.Refresh(refreshIndex);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was refreshed within the collection");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");

        removeActionInvocations.Should().BeEmpty("no items were removed from the collection");
    }

    [Theory]
    [InlineData(1,  0)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  4)]
    public void ItemIsReplaced_RemoveActionIsInvokedForOldItem(
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
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        var replacedItem = source.Items[replacementIndex];
        source.ReplaceAt(
            index:  replacementIndex,
            item:   initialItemCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was replaced within the collection");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");

        removeActionInvocations.Should().ContainSingle("an item was replaced within the collection");
        removeActionInvocations.ElementAt(0).Should().Be(replacedItem, "an item was replaced within the collection");
    }

    [Theory]
    [InlineData(1,  0,  1)]
    [InlineData(5,  0,  1)]
    [InlineData(5,  2,  1)]
    [InlineData(5,  1,  3)]
    [InlineData(5,  0,  5)]
    public void ItemRangeIsRemoved_RemoveActionIsInvokedForEachItem(
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
                removeAction:           removeActionInvocations.Add,
                invokeOnUnsubscribe:    true)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        var removedItems = source.Items
            .Skip(removalIndex)
            .Take(removalCount)
            .ToArray();

        source.RemoveRange(
            index: removalIndex,
            count: removalCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        
        removeActionInvocations.Should().BeEquivalentTo(removedItems, options => options.WithoutStrictOrdering(), "the removal action should be invoked for every removed item");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void ItemsAreCleared_RemoveActionIsInvokedForEachItem(int initialItemCount)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var removeActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRemoved(removeActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        var clearedItems = source.Items
            .ToArray();

        source.Clear();
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("all items in the collection should have been removed");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        
        removeActionInvocations.Should().BeEquivalentTo(clearedItems, options => options.WithoutStrictOrdering(), "the removal action should be invoked for every removed item");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SourceCompletesAsynchronously_CompletionPropagates(bool invokeOnUnsubscribe)
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
                removeAction:           removeActionInvocations.Add,
                invokeOnUnsubscribe:    invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        source.Complete();
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeTrue("the source has completed");
        results.RecordedChangeSets.Should().BeEmpty("no changes were made to the collection");
        
        if (invokeOnUnsubscribe)
            removeActionInvocations.Should().BeEquivalentTo(source.Items, options => options.WithoutStrictOrdering(), "the operator was instructed to invoke the removal action all remaining items, upon stream completion");
        else
            removeActionInvocations.Should().BeEmpty("the operator was instructed to not invoke the removal action, upon stream completion");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SourceCompletesImmediately_CompletionPropagates(bool invokeOnUnsubscribe)
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
                removeAction:           removeActionInvocations.Add,
                invokeOnUnsubscribe:    invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeTrue("the source has completed");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        
        if (invokeOnUnsubscribe)
            removeActionInvocations.Should().BeEquivalentTo(source.Items, options => options.WithoutStrictOrdering(), "the operator was instructed to invoke the removal action all remaining items, upon stream completion");
        else
            removeActionInvocations.Should().BeEmpty("the operator was instructed to not invoke the removal action, upon stream completion");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SourceFailsAsynchronously_CompletionPropagates(bool invokeOnUnsubscribe)
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
                removeAction:           removeActionInvocations.Add,
                invokeOnUnsubscribe:    invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(source.Items, options => options.WithStrictOrdering(), "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        removeActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        var error = new Exception();
        source.SetError(error);
        
        results.Error.Should().BeSameAs(error, "errors within the stream should propagate");
        results.RecordedChangeSets.Should().BeEmpty("no changes were made to the collection");
        
        if (invokeOnUnsubscribe)
            removeActionInvocations.Should().BeEquivalentTo(source.Items, options => options.WithoutStrictOrdering(), "the operator was instructed to invoke the removal action all remaining items, upon stream failure");
        else
            removeActionInvocations.Should().BeEmpty("the operator was instructed to not invoke the removal action, upon stream failure");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SourceFailsImmediately_CompletionPropagates(bool invokeOnUnsubscribe)
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
                removeAction:           removeActionInvocations.Add,
                invokeOnUnsubscribe:    invokeOnUnsubscribe)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeSameAs(error, "errors within the stream should propagate");
        results.RecordedChangeSets.Should().BeEmpty("an error occurred during subscription");
        
        removeActionInvocations.Should().BeEmpty(invokeOnUnsubscribe
            ? "the initial items in the collection were never published"
            : "the operator was instructed to not invoke the removal action, upon stream failure");
    }
}
