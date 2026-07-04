using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;

using FluentAssertions;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public class SourceListFixture
{
    // Covers https://github.com/reactivemarbles/DynamicData/issues/1129
    [Fact]
    public void ConnectDuringEditDoesNotDuplicate()
    {
        using var items = new SourceList<int>();
        
        using var subscriptions = new CompositeDisposable();
        
        // An initial subscription is required to initiate internal buffering of changes, during the upcoming .Edit().
        // That is, we want there to be changes buffered, internally, when the mid-edit subscription comes in, to
        // ensure that they don't get duplicated. This is the scenario that came in up #1129. 
        subscriptions.Add(items
            .Connect()
            .Subscribe());
            
        ListItemRecordingObserver<int>? results = null;
            
        items.Edit(inner =>
        {
            inner.Add(1);

            subscriptions.Add(items
                .Connect()
                .ValidateChangeSets()
                .RecordListItems(out results));
        
            results.Error.Should().BeNull("no errors should have occurred");
            results.RecordedChangeSets.Should().BeEmpty("no changes should be published in the middle of an edit");
        
            inner.Add(2);

            results.Error.Should().BeNull("no errors should have occurred");
            results.RecordedChangeSets.Should().BeEmpty("no changes should be published in the middle of an edit");
        });
        
        results.Should().NotBeNull("the edit delegate should have been invoked");
        results.Error.Should().BeNull("no errors should have occurred");
        results.RecordedChangeSets.Should().ContainSingle("subscribers should only receive a single initial changeset");
        results.RecordedItems.Should().BeEquivalentTo(
            new[] { 1, 2, },
            options => options.WithStrictOrdering(),
            "all items in the source should have propagated downstream");

        results.HasCompleted.Should().BeFalse("the source has not yet completed");
    }

    [Fact]
    public void InitialChangeIsRange()
    {
        var source = new SourceList<string>();
        source.Add("A");
        var changeSets = new List<IChangeSet<string>>();

        source.Connect().Subscribe(changeSets.Add).Dispose();


        changeSets[0].First().Type.Should().Be(ChangeType.Range);
        changeSets[0].First().Range.Index.Should().Be(0);
    }
}
