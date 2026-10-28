namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Fact]
    public void RemoveKeyThenTransformAsync_AddValueTypeToUnsortedCache_AppendsItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        var added = _randomizer.ArrayElement(entries);
        source.AddOrUpdate(entries.Where(entry => entry != added));

        using var subscription = source.Connect()
            .RemoveKey()
            .TransformAsync(static entry => Task.FromResult(entry.Value))
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        var expected = results.RecordedItems.Append(added.Value).ToArray();

        // Act
        source.AddOrUpdate(added);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(expected);
    }

    [Fact]
    public void RemoveKeyThenTransformAsync_UpdateValueTypeInUnsortedCache_ReplacesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var update = GenerateUpdate(_randomizer.ArrayElement(entries), entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .TransformAsync(static entry => Task.FromResult(entry.Value))
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
    public void RemoveKeyThenTransformAsync_RefreshInUnsortedCache_KeepsItemInPlace()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var refreshed = _randomizer.ArrayElement(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .TransformAsync(static entry => Task.FromResult(entry.Value))
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        var expected = results.RecordedItems.ToArray();

        // Act
        source.Refresh(refreshed);

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(expected);
    }

    [Fact]
    public void RemoveKeyThenTransformAsync_RemoveValueTypeFromUnsortedCache_RemovesItem()
    {
        // Arrange
        using var source = new SourceCache<Entry, int>(static entry => entry.Id);
        var entries = GenerateEntries();
        source.AddOrUpdate(entries);
        var removed = _randomizer.ArrayElement(entries);

        using var subscription = source.Connect()
            .RemoveKey()
            .TransformAsync(static entry => Task.FromResult(entry.Value))
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
    public void UnindexedRemove_TransformAsync_RemovesFirstEqualValue()
    {
        // Arrange
        var values = GenerateDistinctValues(2);
        var (duplicated, other) = (values[0], values[1]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .TransformAsync(static item => Task.FromResult(item))
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
    public void UnindexedRemoveRange_TransformAsync_RemovesFirstEqualValues()
    {
        // Arrange
        var values = GenerateDistinctValues(2);
        var (first, second) = (values[0], values[1]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .TransformAsync(static item => Task.FromResult(item))
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
    public void UnindexedReplace_TransformAsync_ReplacesFirstEqualPreviousValueInPlace()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (duplicated, other, replacement) = (values[0], values[1], values[2]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .TransformAsync(static item => Task.FromResult(item))
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<int> { Add(other), Add(duplicated), Add(duplicated) });

        // Act
        source.OnNext(new ChangeSet<int> { Replace(duplicated, replacement) });

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().Equal(other, replacement, duplicated);
    }

    [Fact]
    public void UnindexedRemoveOfEqualInstance_TransformAsync_RemovesItem()
    {
        // Arrange
        var values = GenerateDistinctValues(2);
        var added = new Entity(values[0], values[1]);

        using var source = new Subject<IChangeSet<Entity>>();
        using var subscription = source
            .TransformAsync(static entity => Task.FromResult(entity.Value))
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
    public void UnindexedReplaceOfAbsentItem_TransformAsync_ThrowsUnspecifiedIndexException()
    {
        // Arrange
        var values = GenerateDistinctValues(3);
        var (present, absent, replacement) = (values[0], values[1], values[2]);

        using var source = new Subject<IChangeSet<int>>();
        using var subscription = source
            .TransformAsync(static item => Task.FromResult(item))
            .ValidateSynchronization()
            .RecordListItems(out var results);

        source.OnNext(new ChangeSet<int> { Add(present) });

        // Act
        source.OnNext(new ChangeSet<int> { Replace(absent, replacement) });

        // Assert
        results.Error.Should().BeOfType<UnspecifiedIndexException>();
    }
}
