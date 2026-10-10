using System.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Fact]
    public void RemoveKeyThenFilter_RemoveFromUnsortedCache_RemovesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = GenerateIncludedEntry(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .Filter(static entry => entry.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(static entry => entry.IsIncluded));
        results.RecordedItems.Should().NotContain(removed);
    }

    [Fact]
    public void RemoveKeyThenFilter_UpdateInUnsortedCache_ReplacesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var previous = GenerateIncludedEntry(entries);
        var current = GenerateUpdate(previous, entries) with { IsIncluded = true };

        using var subscription = source.Connect()
            .RemoveKey()
            .Filter(static entry => entry.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.AddOrUpdate(current);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(static entry => entry.IsIncluded));
        results.RecordedItems.Should().Contain(current).And.NotContain(previous);
    }

    [Fact]
    public void UnindexedRemove_Filter_RemovesFirstEqualItem()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (duplicated, other, excluded) = (values[0], values[1], values[2]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .Filter(item => item != excluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<int> { Add(duplicated), Add(excluded), Add(other), Add(duplicated) });

        // Act
        source.OnNext(new ChangeSet<int> { Remove(duplicated) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(other, duplicated);
    }

    [Fact]
    public void UnindexedRemoveRange_Filter_RemovesFirstEqualItems()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (first, second, excluded) = (values[0], values[1], values[2]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .Filter(item => item != excluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<int> { AddRange(first, excluded, second, first, second) });

        // Act
        source.OnNext(new ChangeSet<int> { RemoveRange(second, first) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(first, second);
    }

    [Fact]
    public void UnindexedReplace_Filter_ReplacesFirstEqualPreviousItemInPlace()
    {
        // Arrange
        var values = GenerateDistinctValues(4);
        var (duplicated, other, excluded, replacement) = (values[0], values[1], values[2], values[3]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .Filter(item => item != excluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<int> { Add(excluded), Add(duplicated), Add(other), Add(duplicated) });

        // Act
        source.OnNext(new ChangeSet<int> { Replace(duplicated, replacement) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(replacement, other, duplicated);
    }
}
