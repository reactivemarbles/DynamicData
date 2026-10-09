using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

using DynamicData.Binding;
using DynamicData.Tests.Utilities;

using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Fact]
    public void SortedRemoveKeyThenBind_RefreshThenRemove_KeepsSortedItems()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var refreshed = entries.MinBy(static entry => entry.Value);
        var removed = _randomizer.ArrayElement(entries.Where(entry => entry != refreshed).ToArray());

        using var subscription = source.Connect()
            .Sort(SortExpressionComparer<Entry>.Ascending(static entry => entry.Value))
            .RemoveKey()
            .Bind(out ReadOnlyObservableCollection<Entry> collection)
            .Subscribe();

        // Act
        source.Refresh(refreshed);
        source.RemoveKey(removed.Id);

        // Assert
        collection.Should().Equal(source.Items.OrderBy(static entry => entry.Value));
    }

    [Fact]
    public void RemoveKeyThenAutoRefreshThenFilter_PropertyChangeAfterCacheRefresh_RefreshesChangedItem()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var source = new SourceCache<Person, string>(static person => person.Key);
        source.AddOrUpdate(Enumerable.Range(0, _randomizer.Int(MinimumEntryCount, MaximumEntryCount)).Select(_ => new Person(_randomizer.Hash(), _randomizer.Int(0, threshold * 2))).ToArray());

        var people = source.Items.ToArray();
        var refreshed = people[0];
        var changed = _randomizer.ArrayElement(people.Skip(1).ToArray());
        changed.Age = threshold;

        using var subscription = source.Connect()
            .RemoveKey()
            .AutoRefresh(static person => person.Age)
            .Filter(person => person.Age > threshold)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.Refresh(refreshed);
        changed.Age = threshold + 1;

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(person => person.Age > threshold));
        results.RecordedItems.Should().Contain(changed);
    }

    [Fact]
    public void UnindexedReplace_Clone_ReplacesFirstEqualPreviousItemInPlace()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (duplicated, other, replacement) = (values[0], values[1], values[2]);
        var target = new List<int>();

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source.Clone(target).Subscribe();

        source.OnNext(new ChangeSet<int> { Add(duplicated), Add(other), Add(duplicated) });

        // Act
        source.OnNext(new ChangeSet<int> { Replace(duplicated, replacement) });

        // Assert
        target.Should().Equal(replacement, other, duplicated);
    }

    [Fact]
    public void ReplaceWithOnlyPreviousIndex_Clone_ReplacesInPlace()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (first, second, replacement) = (values[0], values[1], values[2]);
        var target = new List<int>();

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source.Clone(target).Subscribe();

        source.OnNext(new ChangeSet<int> { Add(first), Add(second) });

        // Act
        source.OnNext(new ChangeSet<int> { new Change<int>(ListChangeReason.Replace, replacement, DynamicData.Kernel.Optional.Some(first), previousIndex: 0) });

        // Assert
        target.Should().Equal(replacement, second);
    }

    [Fact]
    public void UnindexedReplaceOfAbsentItem_Clone_AddsCurrentItem()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (present, absent, replacement) = (values[0], values[1], values[2]);
        var target = new List<int>();

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source.Clone(target).Subscribe();

        source.OnNext(new ChangeSet<int> { Add(present) });

        // Act
        source.OnNext(new ChangeSet<int> { Replace(absent, replacement) });

        // Assert
        target.Should().Equal(present, replacement);
    }

    [Fact]
    public void UnindexedReplace_BindToObservableCollection_ReplacesInPlace()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (first, second, replacement) = (values[0], values[1], values[2]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source.Bind(out ReadOnlyObservableCollection<int> collection).Subscribe();

        source.OnNext(new ChangeSet<int> { Add(first), Add(second) });

        // Act
        source.OnNext(new ChangeSet<int> { Replace(first, replacement) });

        // Assert
        collection.Should().Equal(replacement, second);
    }

#if SUPPORTS_BINDINGLIST
    [Fact]
    public void SortedRemoveKeyThenBindToBindingList_RefreshThenRemove_KeepsSortedItems()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var refreshed = entries.MinBy(static entry => entry.Value);
        var removed = _randomizer.ArrayElement(entries.Where(entry => entry != refreshed).ToArray());
        var bindingList = new BindingList<Entry>();

        using var subscription = source.Connect()
            .Sort(SortExpressionComparer<Entry>.Ascending(static entry => entry.Value))
            .RemoveKey()
            .Bind(bindingList)
            .Subscribe();

        // Act
        source.Refresh(refreshed);
        source.RemoveKey(removed.Id);

        // Assert
        bindingList.Should().Equal(source.Items.OrderBy(static entry => entry.Value));
    }

    [Fact]
    public void UnindexedReplace_BindToBindingList_ReplacesFirstEqualPreviousItemInPlace()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (duplicated, other, replacement) = (values[0], values[1], values[2]);
        var bindingList = new BindingList<int>();

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source.Bind(bindingList).Subscribe();

        source.OnNext(new ChangeSet<int> { Add(duplicated), Add(other), Add(duplicated) });

        // Act
        source.OnNext(new ChangeSet<int> { Replace(duplicated, replacement) });

        // Assert
        bindingList.Should().Equal(replacement, other, duplicated);
    }
#endif
}
