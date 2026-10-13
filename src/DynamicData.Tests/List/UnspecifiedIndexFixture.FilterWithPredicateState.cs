using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Theory]
    [InlineData(ListFilterPolicy.CalculateDiff)]
    [InlineData(ListFilterPolicy.ClearAndReplace)]
    public void RemoveKeyThenFilterWithPredicateState_EditUnsortedCache_FiltersEveryChange(ListFilterPolicy filterPolicy)
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        var removed = GenerateIncludedEntry(entries);
        var updated = _randomizer.ArrayElement(entries.Where(entry => entry != removed).ToArray());
        var update = GenerateUpdate(updated, entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .Filter(Observable.Return(true), static (isIncluded, entry) => entry.IsIncluded == isIncluded, filterPolicy)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.AddOrUpdate(entries);
        source.RemoveKey(removed.Id);
        source.AddOrUpdate(update);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(static entry => entry.IsIncluded));
    }

    [Theory]
    [InlineData(ListFilterPolicy.CalculateDiff)]
    [InlineData(ListFilterPolicy.ClearAndReplace)]
    public void UnindexedChanges_FilterWithPredicateState_ResolveFirstEqualItems(ListFilterPolicy filterPolicy)
    {
        // Arrange
        var values = GenerateDistinctValues(4);
        var (first, second, excluded, replacement) = (values[0], values[1], values[2], values[3]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .Filter(Observable.Return(excluded), static (state, item) => item != state, filterPolicy)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.OnNext(new ChangeSet<int> { Add(first), Add(excluded), Add(second), Add(first) });
        source.OnNext(new ChangeSet<int> { AddRange(second) });
        source.OnNext(new ChangeSet<int> { Remove(first) });
        source.OnNext(new ChangeSet<int> { Replace(second, replacement) });
        source.OnNext(new ChangeSet<int> { RemoveRange(first) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(replacement, second);
    }
}
