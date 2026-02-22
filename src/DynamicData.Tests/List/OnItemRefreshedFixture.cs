using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public class OnItemRefreshedFixture
{
    [Theory]
    [InlineData(0,  0)]
    [InlineData(1,  0)]
    [InlineData(1,  1)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  5)]
    public void ItemIsAdded_RefreshActionIsNotInvoked(
        int initialItemCount,
        int insertionIndex)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        if (initialItemCount is 0)
            results.RecordedChangeSets.Should().BeEmpty("there were no initial items to be published");
        else
            results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
        

        // UUT Action
        source.Insert(
            index:  insertionIndex,
            item:   initialItemCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was refreshed within the collection");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");

        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Theory]
    [InlineData(2,  0,  1)]
    [InlineData(2,  1,  0)]
    [InlineData(5,  0,  4)]
    [InlineData(5,  4,  0)]
    [InlineData(5,  1,  3)]
    [InlineData(5,  3,  1)]
    public void ItemIsMoved_RefreshActionIsNotInvoked(
        int initialItemCount,
        int originalIndex,
        int destinationIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
        

        // UUT Action
        source.Move(
            original:       originalIndex,
            destination:    destinationIndex);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was moved within the collection");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");

        refreshActionInvocations.Should().BeEmpty("no items were removed from the collection");
    }

    [Theory]
    [InlineData(1,  0)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  4)]
    public void ItemIsRefreshed_RefreshActionIsNotInvoked(
        int initialItemCount,
        int refreshIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
        

        // UUT Action
        var refreshedItem = source.Items[refreshIndex];
        source.Refresh(refreshIndex);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was refreshed within the collection");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");

        refreshActionInvocations.Should().BeEquivalentTo(new[] { refreshedItem } ,"1 item were refreshed within the collection");
    }

    [Theory]
    [InlineData(1,  0)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  4)]
    public void ItemIsRemoved_RefreshActionIsInvoked(
        int initialItemCount,
        int removalIndex)
    {
        using var source = new TestSourceList<int>();

        if (initialItemCount is not 0)
            source.AddRange(Enumerable.Range(1, initialItemCount));

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
        

        // UUT Action
        var removedItem = source.Items[removalIndex];
        source.RemoveAt(removalIndex);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was removed from the collection");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");

        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Theory]
    [InlineData(1,  0)]
    [InlineData(5,  0)]
    [InlineData(5,  2)]
    [InlineData(5,  4)]
    public void ItemIsReplaced_RefreshActionIsInvokedForOldItem(
        int initialItemCount,
        int replacementIndex)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
        

        // UUT Action
        source.ReplaceAt(
            index:  replacementIndex,
            item:   initialItemCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("an item was replaced within the collection");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");

        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Theory]
    [InlineData(1,  0,  1)]
    [InlineData(5,  0,  1)]
    [InlineData(5,  2,  1)]
    [InlineData(5,  1,  3)]
    [InlineData(5,  0,  5)]
    public void ItemRangeIsRemoved_RefreshActionIsInvokedForEachItem(
        int initialItemCount,
        int removalIndex,
        int removalCount)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
        

        // UUT Action
        source.RemoveRange(
            index: removalIndex,
            count: removalCount);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle($"{removalCount} item{((removalCount is 1) ? "" : "s")} should have been removed");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void ItemsAreCleared_RefreshActionIsInvokedForEachItem(int initialItemCount)
    {
        using var source = new TestSourceList<int>();

        source.AddRange(Enumerable.Range(1, initialItemCount));

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
        

        // UUT Action
        source.Clear();
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("all items in the collection should have been removed");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Fact]
    public void SourceCompletesAsynchronously_CompletionPropagates()
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        source.Complete();
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeTrue("the source has completed");
        results.RecordedChangeSets.Should().BeEmpty("no changes were made to the collection");
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Fact]
    public void SourceCompletesImmediately_CompletionPropagates()
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });
        source.Complete();
        
        var refreshActionInvocations = new List<int>();
        
        // UUT Construction & Action
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeTrue("the source has completed");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Fact]
    public void SourceFailsAsynchronously_CompletionPropagates()
    {
        using var source = new TestSourceList<int>();

        source.AddRange(new[]
        {
            1,
            2,
            3
        });

        var refreshActionInvocations = new List<int>();
        
        // UUT Construction
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("the source can still publish notifications");
        results.RecordedChangeSets.Should().ContainSingle("the initial items should have been published");
        results.RecordedItems.Should().BeEquivalentTo(
            expectation:    source.Items,
            config:         options => options.WithStrictOrdering(),
            because:        "all collection changes should propagate downstream");
        results.ClearChangeSets();
        
        refreshActionInvocations.Should().BeEmpty("no items have been removed from the collection");
        

        // UUT Action
        var error = new Exception();
        source.SetError(error);
        
        results.Error.Should().BeSameAs(error, "errors within the stream should propagate");
        results.RecordedChangeSets.Should().BeEmpty("no changes were made to the collection");
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }

    [Fact]
    public void SourceFailsImmediately_CompletionPropagates()
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
        
        var refreshActionInvocations = new List<int>();
        
        // UUT Construction & Action
        using var subscription = source.Connect()
            .OnItemRefreshed(refreshActionInvocations.Add)
            .ValidateChangeSets()
            .RecordListItems(out var results);
        
        results.Error.Should().BeSameAs(error, "errors within the stream should propagate");
        results.RecordedChangeSets.Should().BeEmpty("an error occurred during subscription");
        
        refreshActionInvocations.Should().BeEmpty("no items were refreshed within the collection");
    }
}
