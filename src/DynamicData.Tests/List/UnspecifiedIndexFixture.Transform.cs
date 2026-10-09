using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Fact]
    public void RemoveKeyThenTransform_RemoveValueTypeFromUnsortedCache_RemovesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .Transform(static entry => entry.Value)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Select(static entry => entry.Value));
    }

    [Fact]
    public void RemoveKeyThenTransform_UpdateValueTypeInUnsortedCache_ReplacesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var update = GenerateUpdate(_randomizer.ArrayElement(entries), entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .Transform(static entry => entry.Value)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.AddOrUpdate(update);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Select(static entry => entry.Value));
    }

    [Fact]
    public void UnindexedRemove_Transform_RemovesFirstEqualValue()
    {
        // Arrange
        var values = GenerateDistinctValues(2);
        var (duplicated, other) = (values[0], values[1]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .Transform(static item => item)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<int> { Add(duplicated), Add(other), Add(duplicated) });

        // Act
        source.OnNext(new ChangeSet<int> { Remove(duplicated) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(other, duplicated);
    }

    [Fact]
    public void UnindexedRemoveRange_Transform_RemovesFirstEqualValues()
    {
        // Arrange
        var values = GenerateDistinctValues(2);
        var (first, second) = (values[0], values[1]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .Transform(static item => item)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<int> { AddRange(first, second, first, second) });

        // Act
        source.OnNext(new ChangeSet<int> { RemoveRange(second, first) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(first, second);
    }

    [Fact]
    public void UnindexedRemoveOfEqualInstance_Transform_RemovesItem()
    {
        // Arrange
        var values = GenerateDistinctValues(2);
        var added = new Entity(values[0], values[1]);

        using var source = new Subject<IChangeSet<Entity>>();
        using var subscription = source
            .Transform(static entity => entity.Value)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<Entity> { Add(added) });

        // Act
        source.OnNext(new ChangeSet<Entity> { Remove(added with { }) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEmpty();
    }

    [Fact]
    public void RemoveKeyThenDistinctValues_RemoveValueTypeFromUnsortedCache_RemovesValue()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .DistinctValues(static entry => entry.Value)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Select(static entry => entry.Value));
    }

    [Fact]
    public void RemoveKeyThenFilterOnObservablePredicate_RemoveValueTypeFromUnsortedCache_RemovesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = GenerateIncludedEntry(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .Filter(Observable.Return<Func<Entry, bool>>(static entry => entry.IsIncluded))
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(static entry => entry.IsIncluded));
    }

    [Fact]
    public void RemoveKeyThenFilterOnObservable_RemoveValueTypeFromUnsortedCache_RemovesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = GenerateIncludedEntry(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .FilterOnObservable(static entry => Observable.Return(entry.IsIncluded))
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Where(static entry => entry.IsIncluded));
    }

    [Fact]
    public void RemoveKeyThenGroupWithImmutableState_RemoveValueTypeFromUnsortedCache_RemovesItemFromGroup()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);

        using var groups = source.Connect()
            .RemoveKey()
            .GroupWithImmutableState(static entry => entry.IsIncluded)
            .AsObservableList();

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        groups.Items.SelectMany(static group => group.Items).Should().BeEquivalentTo(source.Items);
    }

    [Fact]
    public void RemoveKeyThenMergeManyChangeSets_RemoveValueTypeFromUnsortedCache_RemovesChildren()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .MergeManyChangeSets(static entry => Observable.Return<IChangeSet<int>>(new ChangeSet<int> { Add(entry.Value) }))
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.Select(static entry => entry.Value));
    }

    [Fact]
    public void RemoveKeyThenGroupOn_RemoveValueTypeFromUnsortedCache_RemovesItemFromGroup()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);

        using var groups = source.Connect()
            .RemoveKey()
            .GroupOn(static entry => entry.IsIncluded)
            .AsObservableList();

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        groups.Items.SelectMany(static group => group.List.Items).Should().BeEquivalentTo(source.Items);
    }

    [Fact]
    public void RemoveKeyThenTransformMany_RemoveValueTypeFromUnsortedCache_RemovesChildren()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .TransformMany(static entry => new[] { entry.Id, entry.Value })
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(source.Items.SelectMany(static entry => new[] { entry.Id, entry.Value }));
    }

    [Fact]
    public void RemoveKeyThenSubscribeMany_RemoveValueTypeFromUnsortedCache_DisposesSubscription()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);
        var disposed = new List<Entry>();

        using var subscription = source.Connect()
            .RemoveKey()
            .SubscribeMany(entry => Disposable.Create(() => disposed.Add(entry)))
            .Subscribe();

        // Act
        source.RemoveKey(removed.Id);

        // Assert
        disposed.Should().Equal(removed);
    }
}
