using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DynamicData.Tests.List;

public class CloneChangesFixture
{
    private readonly ITestOutputHelper _output;
    
    public CloneChangesFixture(ITestOutputHelper output)
        => _output = output;
    
    public class ChangesAreInvalid_TestCase
    {
        public required IReadOnlyList<Change<int>> Changes { get; init; }

        public required IReadOnlyList<int> InitialItems { get; init; }
        
        public required string Name { get; init; }

        public override string ToString()
            => Name;
    }
    
    public static readonly TheoryData<ChangesAreInvalid_TestCase> ChangesAreInvalid_TestCases
        = new()
        {
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Add, current: 4, index: 4) },
                Name            = "Add at out-of-bounds index"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.AddRange, items: new[] { 4, 5, 6 }, index: 4) },
                Name            = "Add range at out-of-bounds index"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(current: 2, previousIndex: 1, currentIndex: 3) },
                Name            = "Move to out-of-bounds index"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(current: 3, previousIndex: 3, currentIndex: 2) },
                Name            = "Move from out-of-bounds index"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Refresh, current: 4, index: 3) },
                Name            = "Refresh at out-of-bounds index"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = Array.Empty<int>(),
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 1, index: 0) },
                Name            = "Remove from empty source"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 2, index: 3) },
                Name            = "Remove at out-of-bounds index"
            },
            // TODO: Decide whether this is desired behavior, and fix in .Clone() if so
            // new ChangesAreInvalid_TestCase()
            // {
            //     InitialItems    = new[] { 1, 2, 3 },
            //     Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 4, index: -1) },
            //     Name            = "Remove for item not in source"
            // },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = Array.Empty<int>(),
                Changes         = new[] { new Change<int>(reason: ListChangeReason.RemoveRange, items: new[] { 1, 2, 3 }, index: 0) },
                Name            = "Remove Range from empty source"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.RemoveRange, items: new[] { 4, 5, 6 }, index: 4) },
                Name            = "Remove Range at out-of-bounds index"
            },
            new ChangesAreInvalid_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.RemoveRange, items: new[] { 2, 3, 4 }, index: 2) },
                Name            = "Remove Range too large"
            },
            // TODO: Decide whether this is desired behavior, and fix in .Clone() if so
            // new ChangesAreInvalid_TestCase()
            // {
            //     InitialItems    = Array.Empty<int>(),
            //     Changes         = new[] { new Change<int>(reason: ListChangeReason.Replace, current: 2, previous: 1, currentIndex: 0) },
            //     Name            = "Replace upon empty source"
            // },
            // TODO: Decide whether this is desired behavior, and fix in .Clone() if so
            // new ChangesAreInvalid_TestCase()
            // {
            //     InitialItems    = new[] { 1, 2, 3 },
            //     Changes         = new[] { new Change<int>(reason: ListChangeReason.Replace, current: 5, previous: 4, currentIndex: 3) },
            //     Name            = "Replace at invalid index"
            // }
        };

    [Theory]
    [MemberData(nameof(ChangesAreInvalid_TestCases))]
    public void ChangesAreInvalid_ThrowsException(ChangesAreInvalid_TestCase testCase)
    {
        var source = testCase.InitialItems.ToList();
        
        var result = FluentActions.Invoking(() => source.Clone(
                changes:            testCase.Changes,
                equalityComparer:   null))
            .Should().Throw<Exception>()
            .Which;
        
        _output.WriteLine(result.ToString());
    }
    
    public class ChangesDoNotMutateSource_TestCase
    {
        public required IReadOnlyList<Change<int>> Changes { get; init; }

        public required IReadOnlyList<int> InitialItems { get; init; }
        
        public required string Name { get; init; }

        public override string ToString()
            => Name;
    }

    public static readonly TheoryData<ChangesDoNotMutateSource_TestCase> ChangesDoNotMutateSource_TestCases
        = new()
        {
            new ChangesDoNotMutateSource_TestCase()
            {
                InitialItems    = Array.Empty<int>(),
                Changes         = Array.Empty<Change<int>>(),
                Name            = "Empty changeset, Empty source"
            },
            new ChangesDoNotMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = Array.Empty<Change<int>>(),
                Name            = "Empty changeset, Populated source"
            },
            new ChangesDoNotMutateSource_TestCase()
            {
                InitialItems    = Array.Empty<int>(),
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Clear, items: Array.Empty<int>()) },
                Name            = "Clear for empty source"
            },
            new ChangesDoNotMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(current: 1, previousIndex: 0, currentIndex: 0) },
                Name            = "Move for same index, front of source"
            },
            new ChangesDoNotMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(current: 2, previousIndex: 1, currentIndex: 1) },
                Name            = "Move for same index, middle of source"
            },
            new ChangesDoNotMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(current: 3, previousIndex: 2, currentIndex: 2) },
                Name            = "Move for same index, end of source"
            }
        };
    
    [Theory]
    [MemberData(nameof(ChangesDoNotMutateSource_TestCases))]
    public void ChangesDoNotMutateSource_SourceDoesNotChange(ChangesDoNotMutateSource_TestCase testCase)
    {
        var source = testCase.InitialItems.ToList();
        
        source.Clone(
            changes:            testCase.Changes,
            equalityComparer:   null);
        
        source.Should().BeEquivalentTo(
            expectation:    testCase.InitialItems,
            config:         options => options.WithStrictOrdering());
    }
    
    [Fact]
    public void ChangesIsNull_ThrowsException()
    {
        var source = new List<int>();
        
        var result = FluentActions.Invoking(() => source.Clone(
                changes:            null!,
                equalityComparer:   null))
            .Should().Throw<ArgumentNullException>()
            .Which;
            
        result.ParamName.Should().Be("changes");
        
        _output.WriteLine(result.ToString());
    }
    
    public class ChangesMutateSource_TestCase
    {
        public required IReadOnlyList<Change<int>> Changes { get; init; }

        public required IReadOnlyList<int> FinalItems { get; init; }

        public required IReadOnlyList<int> InitialItems { get; init; }
        
        public required string Name { get; init; }

        public override string ToString()
            => Name;
    }

    public static readonly TheoryData<ChangesMutateSource_TestCase> ChangesMutateSource_TestCases
        = new()
        {
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = Array.Empty<int>(),
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Add, current: 1, index: 0) },
                FinalItems      = new[] { 1 },
                Name            = "Add item to empty source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Add, current: 4, index: 0) },
                FinalItems      = new[] { 4, 1, 2, 3 },
                Name            = "Add item at front of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Add, current: 4, index: 1) },
                FinalItems      = new[] { 1, 4, 2, 3 },
                Name            = "Add item to middle of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Add, current: 4, index: 3) },
                FinalItems      = new[] { 1, 2, 3, 4 },
                Name            = "Add item at end of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Add, current: 4, index: -1) },
                FinalItems      = new[] { 1, 2, 3, 4 },
                Name            = "Add item at unspecified index"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = Array.Empty<int>(),
                Changes         = new[] { new Change<int>(reason: ListChangeReason.AddRange, items: new[] { 1, 2, 3 }, index: 0) },
                FinalItems      = new[] { 1, 2, 3 },
                Name            = "Add range to empty source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.AddRange, items: new[] { 4, 5, 6 }, index: 0) },
                FinalItems      = new[] { 4, 5, 6, 1, 2, 3 },
                Name            = "Add range at front of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.AddRange, items: new[] { 4, 5, 6 }, index: 1) },
                FinalItems      = new[] { 1, 4, 5, 6, 2, 3 },
                Name            = "Add range to middle of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.AddRange, items: new[] { 4, 5, 6 }, index: 3) },
                FinalItems      = new[] { 1, 2, 3, 4, 5, 6 },
                Name            = "Add range at end of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.AddRange, items: new[] { 4, 5, 6 }, index: -1) },
                FinalItems      = new[] { 1, 2, 3, 4, 5, 6 },
                Name            = "Add range at unspecified index"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Clear, items: new[] { 1 }, index: 0) },
                FinalItems      = Array.Empty<int>(),
                Name            = "Clear single item"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Clear, items: new[] { 1, 2, 3 }, index: 0) },
                FinalItems      = Array.Empty<int>(),
                Name            = "Clear multiple items"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(current: 1, previousIndex: 0, currentIndex: 2) },
                FinalItems      = new[] { 2, 3, 1 },
                Name            = "Move from front to end of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(current: 3, previousIndex: 2, currentIndex: 0) },
                FinalItems      = new[] { 3, 1, 2 },
                Name            = "Move from end to front of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3, 4 },
                Changes         = new[] { new Change<int>(current: 2, previousIndex: 1, currentIndex: 2) },
                FinalItems      = new[] { 1, 3, 2, 4 },
                Name            = "Move item forward to adjacent position"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3, 4 },
                Changes         = new[] { new Change<int>(current: 3, previousIndex: 2, currentIndex: 1) },
                FinalItems      = new[] { 1, 3, 2, 4 },
                Name            = "Move item backward to adjacent position"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 1, index: 0) },
                FinalItems      = Array.Empty<int>(),
                Name            = "Remove last item from source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 1, index: 0) },
                FinalItems      = new[] { 2, 3 },
                Name            = "Remove item from front of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 2, index: 1) },
                FinalItems      = new[] { 1, 3 },
                Name            = "Remove item from middle of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 3, index: 2) },
                FinalItems      = new[] { 1, 2 },
                Name            = "Remove item from end of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Remove, current: 2, index: -1) },
                FinalItems      = new[] { 1, 3 },
                Name            = "Remove item at unspecified index"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.RemoveRange, items: new[] { 1, 2, 3 }, index: 0) },
                FinalItems      = Array.Empty<int>(),
                Name            = "Remove entire range from source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3, 4, 5, 6 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.RemoveRange, items: new[] { 1, 2, 3 }, index: 0) },
                FinalItems      = new[] { 4, 5, 6 },
                Name            = "Remove range from front of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3, 4, 5, 6 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.RemoveRange, items: new[] { 2, 3, 4 }, index: 1) },
                FinalItems      = new[] { 1, 5, 6 },
                Name            = "Remove range from middle of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3, 4, 5, 6 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.RemoveRange, items: new[] { 4, 5, 6 }, index: 3) },
                FinalItems      = new[] { 1, 2, 3 },
                Name            = "Remove range from end of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Replace, previous: 1, current: 4, currentIndex: 0) },
                FinalItems      = new[] { 4, 2, 3 },
                Name            = "Replace item at front of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Replace, previous: 2, current: 4, currentIndex: 1) },
                FinalItems      = new[] { 1, 4, 3 },
                Name            = "Replace item in middle of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = new[] { 1, 2, 3 },
                Changes         = new[] { new Change<int>(reason: ListChangeReason.Replace, previous: 3, current: 4, currentIndex: 2) },
                FinalItems      = new[] { 1, 2, 4 },
                Name            = "Replace item at end of source"
            },
            new ChangesMutateSource_TestCase()
            {
                InitialItems    = Array.Empty<int>(),
                Changes         = new Change<int>[]
                {
                    new(reason: ListChangeReason.AddRange,  items: new[] { 1, 2, 3 }),
                    new(reason: ListChangeReason.Remove,    current: 2, index: 1),
                    new(reason: ListChangeReason.Add,       current: 4),
                    new(reason: ListChangeReason.Add,       current: 5, index: 1 ),
                    new(                                    current: 1, previousIndex: 0, currentIndex: 2),
                    new(reason: ListChangeReason.Replace,   previous: 3, current: 4, currentIndex: 1),
                    new(reason: ListChangeReason.Replace,   previous: 5, current: 1, currentIndex: 0)
                },
                FinalItems      = new[] { 1, 4, 1, 4 },
                Name            = "Complex sequence of changes"
            }
        };
    
    [Theory]
    [MemberData(nameof(ChangesMutateSource_TestCases))]
    public void ChangesMutateSource_SourceMatchesExpected(ChangesMutateSource_TestCase testCase)
    {
        var source = testCase.InitialItems.ToList();
        
        source.Clone(
            changes:            testCase.Changes,
            equalityComparer:   null);
        
        source.Should().BeEquivalentTo(
            expectation:    testCase.FinalItems,
            config:         options => options.WithStrictOrdering());
    }
    
    [Fact]
    public void EqualityComparerIsGiven_EqualityComparerIsUsedForSearching()
    {
        var source = new List<string>()
        {
            "First",
            "Second",
            "Third"
        };
        
        source.Clone(
            changes:            new[] { new Change<string>(reason: ListChangeReason.Remove, current: "second") },
            equalityComparer:   StringComparer.OrdinalIgnoreCase);
        
        source.Should().BeEquivalentTo(
            expectation:    new[] { "First", "Third" },
            config:         options => options.WithStrictOrdering());
    }
    
    // TODO: Decide whether this is desired behavior, and fix in .Clone() if so
    // [Fact]
    // public void SourceDoesNotSupportRefresh_RefreshChangeDoesNotPropagate()
    // {
    //     var initialItems = new[] { 1, 2, 3 };
    //     
    //     var source = new ObservableCollection<int>(initialItems);
    //     
    //     using var collectionChangedSubscription = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
    //             addHandler:     handler => source.CollectionChanged += handler,
    //             removeHandler:  handler => source.CollectionChanged -= handler)
    //         .RecordValues(out var collectionChangedResults); 
    //     
    //     source.Clone(
    //         changes:            new[]
    //         {
    //             new Change<int>(
    //                 reason:     ListChangeReason.Refresh,
    //                 current:    2,
    //                 index:      1)
    //         },
    //         equalityComparer:   null);
    //     
    //     collectionChangedResults.RecordedValues.Should().BeEmpty("A Refresh change cannot be applied to a target that does not support it");
    //     
    //     source.Should().BeEquivalentTo(
    //         expectation:    initialItems,
    //         config:         options => options.WithStrictOrdering());
    // }
    
    [Fact]
    public void SourceIsNull_ThrowsException()
    {
        var result = FluentActions.Invoking(() => (null as IList<int>)!.Clone(
                changes:            Enumerable.Empty<Change<int>>(),
                equalityComparer:   null))
            .Should().Throw<ArgumentNullException>()
            .Which;
            
        result.ParamName.Should().Be("source");
        
        _output.WriteLine(result.ToString());
    }
    
    [Fact]
    public void SourceSupportsRefresh_RefreshChangesPropagate()
    {
        var initialItems = new[] { 1, 2, 3 };
        
        var source = new ChangeAwareList<int>(initialItems);
        source.CaptureChanges(); 
        
        var changes = new[]
        {
            new Change<int>(
                reason:     ListChangeReason.Refresh,
                current:    2,
                index:      1)
        };
        
        source.Clone(
            changes:            changes,
            equalityComparer:   null);
        
        var capturedChanges = source.CaptureChanges();
        capturedChanges.Should().BeEquivalentTo(
            expectation:    changes,
            config:         options => options.WithStrictOrdering());
        
        source.Should().BeEquivalentTo(
            expectation:    initialItems,
            config:         options => options.WithStrictOrdering());
    }
}
