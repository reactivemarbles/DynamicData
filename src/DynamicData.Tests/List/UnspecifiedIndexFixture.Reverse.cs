using System.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Fact]
    public void RemoveKeyThenReverse_EditUnsortedCache_ReversesItems()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        var removed = _randomizer.ArrayElement(entries);
        var update = GenerateUpdate(_randomizer.ArrayElement(entries.Where(entry => entry != removed).ToArray()), entries);

        using var forward = source.Connect().RemoveKey().AsObservableList();
        using var reversed = source.Connect().RemoveKey().Reverse().AsObservableList();

        // Act
        source.AddOrUpdate(entries);
        source.RemoveKey(removed.Id);
        source.AddOrUpdate(update);

        // Assert
        reversed.Items.Should().Equal(forward.Items.Reverse());
    }

    [Fact]
    public void UnindexedChanges_Reverse_EmitPositionsInReversedList()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (first, second, replacement) = (values[0], values[1], values[2]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .Reverse()
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act
        source.OnNext(new ChangeSet<int> { Add(first), Add(second) });
        source.OnNext(new ChangeSet<int> { Replace(second, replacement), Remove(first), RemoveRange(replacement) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedValues.Should().HaveCount(2);
        results.RecordedValues[0].Select(static change => (change.Reason, change.Item.Current, change.Item.CurrentIndex)).Should().Equal(
            (ListChangeReason.Add, first, 0),
            (ListChangeReason.Add, second, 0));
        results.RecordedValues[1].Select(static change => (change.Reason, Index: (change.Type is ChangeType.Item) ? change.Item.CurrentIndex : change.Range.Index)).Should().Equal(
            (ListChangeReason.Replace, -1),
            (ListChangeReason.Remove, -1),
            (ListChangeReason.RemoveRange, -1));
        results.RecordedValues[1].First().Item.PreviousIndex.Should().Be(-1);
    }
}
