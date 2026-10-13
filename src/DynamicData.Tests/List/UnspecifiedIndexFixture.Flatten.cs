using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Fact]
    public void UnindexedRanges_Flatten_EmitUnindexedItemChanges()
    {
        // Arrange
        var values = GenerateDistinctValues(4);
        var (inserted, firstAdded, secondAdded, removed) = (values[0], values[1], values[2], values[3]);
        var insertIndex = _randomizer.Int(1, MaximumEntryCount);
        var changes = new ChangeSet<int>
        {
            new Change<int>(ListChangeReason.Add, inserted, insertIndex),
            AddRange(firstAdded, secondAdded),
            RemoveRange(removed),
        };

        // Act
        var flattened = changes.Flatten().Select(static change => (change.Reason, change.Current, change.CurrentIndex)).ToArray();

        // Assert
        flattened.Should().Equal(
            (ListChangeReason.Add, inserted, insertIndex),
            (ListChangeReason.Add, firstAdded, -1),
            (ListChangeReason.Add, secondAdded, -1),
            (ListChangeReason.Remove, removed, -1));
    }

    [Fact]
    public void ClearAfterIndexedChange_Flatten_IndexesFromStartOfList()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var insertIndex = _randomizer.Int(1, MaximumEntryCount);
        var changes = new ChangeSet<int>
        {
            new Change<int>(ListChangeReason.Add, values[0], insertIndex),
            new Change<int>(ListChangeReason.Clear, values),
        };

        // Act
        var flattened = changes.Flatten().Skip(1).Select(static change => (change.Reason, change.Current, change.CurrentIndex)).ToArray();

        // Assert
        flattened.Should().Equal(values.Select(static (value, index) => (ListChangeReason.Remove, value, index)));
    }

    [Fact]
    public void WhereReasonsAreThenForEachItemChange_InsertThenAddRange_ReportsUnspecifiedIndexes()
    {
        // Arrange
        var values = GenerateDistinctValues(4);
        using var source = new SourceList<int>();
        source.Add(values[0]);
        var observed = new List<ItemChange<int>>();

        using var subscription = source.Connect()
            .WhereReasonsAre(ListChangeReason.Add, ListChangeReason.AddRange)
            .ForEachItemChange(observed.Add)
            .Subscribe();
        observed.Clear();

        // Act
        source.Edit(list =>
        {
            list.Insert(0, values[1]);
            list.AddRange(values.Skip(2));
        });

        // Assert
        observed.Select(static change => (change.Current, change.CurrentIndex)).Should().Equal(values.Skip(1).Select(static value => (value, -1)));
    }
}
