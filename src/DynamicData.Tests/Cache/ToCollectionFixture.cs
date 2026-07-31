using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public class ToCollectionFixture
{
    public ToCollectionFixture(ITestOutputHelper output)
        => _output = output;

    public record TestItem
    {
        public static int SelectId(TestItem item)
            => item.Id;
    
        public required int Id { get; init; }
        
        public int Version { get; init; }
    }

    [Fact]
    public void WhenChangesAreMade_ResultMatchesSourceAndPriorResultsAreNotMutated()
    {
        // Setup
        using var source = new SourceCache<TestItem, int>(TestItem.SelectId);


        // UUT Initialization
        var priorResults = new List<IReadOnlyCollection<TestItem>>();

        using var subscription = source.Connect()
            .ToCollection()
            .RecordValues(out var results);
            
        results.Error.Should().BeNull("no errors should have occurred");
        // TODO: Disabled due to existing defect. Fix and restore.
        //results.RecordedValues.Should().ContainSingle("an initial snapshot should always be published");
        //results.RecordedValues[^1].Should().BeEmpty("no items have been added to the source");
        results.HasCompleted.Should().BeFalse("the source has not completed");

        // TODO: Disabled due to existing defect. Fix and restore.
        //priorResults.Add(results.RecordedValues[^1].ToArray());
        

        // UUT Action (add items)
        source.AddOrUpdate(new[]
        {
            new TestItem() { Id = 1 },
            new TestItem() { Id = 2 },
            new TestItem() { Id = 3 }
        });
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.RecordedValues.Skip(priorResults.Count).Should().ContainSingle("a single source operation was performed");
        results.RecordedValues[^1].Should().BeEquivalentTo(source.Items, "snapshots should always match the source collection");
        results.HasCompleted.Should().BeFalse("the source has not completed");

        foreach (var (result, priorResult) in results.RecordedValues.Zip(priorResults))
            result.Should().BeEquivalentTo(priorResult, "previous snapshots should not be mutated");

        priorResults.Add(results.RecordedValues[^1].ToArray());
        
        
        // UUT Action (replace items)
        source.AddOrUpdate(new[]
        {
            new TestItem() { Id = 1, Version = 1 },
            new TestItem() { Id = 2, Version = 1 },
            new TestItem() { Id = 3, Version = 1 }
        });
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.RecordedValues.Skip(priorResults.Count).Should().ContainSingle("a single source operation was performed");
        results.RecordedValues[^1].Should().BeEquivalentTo(source.Items, "snapshots should always match the source collection");
        results.HasCompleted.Should().BeFalse("the source has not completed");

        foreach (var (result, priorResult) in results.RecordedValues.Zip(priorResults))
            result.Should().BeEquivalentTo(priorResult, "previous snapshots should not be mutated");

        priorResults.Add(results.RecordedValues[^1].ToArray());


        // UUT Action (remove items)
        source.RemoveKeys(source.Keys.ToArray());
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.RecordedValues.Skip(priorResults.Count).Should().ContainSingle("a single source operation was performed");
        results.RecordedValues[^1].Should().BeEquivalentTo(source.Items, "snapshots should always match the source collection");
        results.HasCompleted.Should().BeFalse("the source has not completed");

        foreach (var (result, priorResult) in results.RecordedValues.Zip(priorResults))
            result.Should().BeEquivalentTo(priorResult, "previous snapshots should not be mutated");

        priorResults.Add(results.RecordedValues[^1].ToArray());
    }

    [Theory]
    [InlineData(StreamCompletionStrategy.Asynchronous)]
    [InlineData(StreamCompletionStrategy.Immediate)]
    public void WhenSourceCompletes_CompletionPropagates(StreamCompletionStrategy completionStrategy)
    {
        // Setup
        using var source = new TestSourceCache<TestItem, int>(TestItem.SelectId);


        // UUT Initialization & Action
        if (completionStrategy is StreamCompletionStrategy.Immediate)
            source.Complete();

        using var subscription = source.Connect(suppressEmptyChangeSets: false)
            .ToCollection()
            .RecordValues(out var results);

        if (completionStrategy is StreamCompletionStrategy.Asynchronous)
            source.Complete();

        results.Error.Should().BeNull();
        results.RecordedValues.Should().ContainSingle("an initial snapshot should always be published");
        results.RecordedValues[^1].Should().BeEmpty("no items were added to the source");
        results.HasCompleted.Should().BeTrue("the source has completed");
    }

    [Theory]
    [InlineData(StreamCompletionStrategy.Asynchronous)]
    [InlineData(StreamCompletionStrategy.Immediate)]
    public void WhenSourceFails_ErrorPropagates(StreamCompletionStrategy completionStrategy)
    {
        // Setup
        using var source = new TestSourceCache<TestItem, int>(TestItem.SelectId);


        // UUT Initialization & Action
        var error = new Exception("Test");

        if (completionStrategy is StreamCompletionStrategy.Immediate)
            source.SetError(error);

        using var subscription = source.Connect()
            .ToCollection()
            .RecordValues(out var results);

        if (completionStrategy is StreamCompletionStrategy.Asynchronous)
            source.SetError(error);

        results.Error.Should().Be(error, "errors should propagate");
        // TODO: Disabled due to existing defect. Fix and restore.
        //results.RecordedValues.Should().ContainSingle("an initial snapshot should always be published");
        //results.RecordedValues[^1].Should().BeEmpty("no items were added to the source");
    }

    [Fact]
    public void WhenSourceIsNull_ThrowsException()
    {
        // UUT Action
        var result = FluentActions.Invoking(() =>
            {
                _ = ObservableCacheEx.ToCollection<int, int>(null!);
            })
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("source")
            .Which;
            
        _output.WriteLine(result.ToString());
    }

    [Fact]
    public void WhenSubscriptionIsDisposed_SubscriptionDisposalPropagates()
    {
        // Setup
        using var source = new Subject<IChangeSet<Item, int>>();


        // UUT Initialization
        using var subscription = source
            .ToCollection()
            .RecordValues(out var results);

        results.Error.Should().BeNull();
        // TODO: Disabled due to existing defect. Fix and restore.
        //results.RecordedValues.Should().ContainSingle("an initial snapshot should always be published");
        //results.RecordedValues[^1].Should().BeEmpty("no items were added to the source");
        results.HasCompleted.Should().BeFalse("the source has not completed");


        // UUT Action
        subscription.Dispose();

        source.HasObservers.Should().BeFalse("subscription disposal should propagate to the source");
    }

    private readonly ITestOutputHelper _output;
}
