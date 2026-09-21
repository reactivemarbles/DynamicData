using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using Bogus;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public partial class RemoveKeyFixture
{
    private const int IdentitySeed = 0x1165;

    private readonly Randomizer _identityRandomizer = new(IdentitySeed);

    /// <summary>
    /// Refreshing one cache key must re-filter its own list slot, including when an earlier value compares equal.
    /// The contract is the same for an initial snapshot and for additions after subscription.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualValuesAreRefreshed_FilterRemovesTheChangedKey(bool itemsExistBeforeSubscription)
    {
        // Arrange: equal values with distinct keys and different inclusion states.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue, isIncluded: false);
        var second = CreateEqualItem(equalityValue);
        var items = new[] { first, second };

        Assert.NotEqual(first.Key, second.Key);
        Assert.NotSame(first, second);
        Assert.Equal(first, second);

        if (itemsExistBeforeSubscription)
            source.AddOrUpdate(items);

        using var subscription = source.Connect()
            .RemoveKey()
            .Filter(static item => item.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .ToCollection()
            .RecordValues(out var results);

        if (!itemsExistBeforeSubscription)
            source.AddOrUpdate(items);

        // Assert the baseline before refreshing the included key.
        Assert.Null(results.Error);
        Assert.NotEmpty(results.RecordedValues);
        Assert.Same(second, Assert.Single(results.RecordedValues[^1]));

        // Act: only the second key is refreshed, and neither item now matches.
        second.IsIncluded = false;
        source.Refresh(new[] { second });

        // Assert: no stale equal item survives in the materialized collection.
        Assert.All(source.Items, static item => Assert.False(item.IsIncluded));
        Assert.Null(results.Error);
        Assert.Empty(results.RecordedValues[^1]);
        Assert.False(results.HasCompleted);
    }

    /// <summary>
    /// Two keys containing the identical reference must still retain separate inclusion states.
    /// Refreshing both keys must remove both list occurrences, not repeatedly re-filter the first slot.
    /// </summary>
    [Fact]
    public void SharedReferenceIsRefreshed_FilterTracksEachKeySeparately()
    {
        // Arrange: a transform retains two source keys while projecting the same object for both.
        using var source = new TestSourceCache<KeyValuePair<Guid, EqualItem>, Guid>(static entry => entry.Key);
        var shared = CreateEqualItem(_identityRandomizer.Int());
        var first = new KeyValuePair<Guid, EqualItem>(_identityRandomizer.Guid(), shared);
        var second = new KeyValuePair<Guid, EqualItem>(_identityRandomizer.Guid(), shared);
        Assert.NotEqual(first.Key, second.Key);

        using var subscription = source.Connect()
            .Transform(static entry => entry.Value)
            .RemoveKey()
            .Filter(static item => item.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.AddOrUpdate(new[] { first, second });
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(shared, item),
            item => Assert.Same(shared, item));

        // Act: both keyed refreshes are delivered in one changeset.
        shared.IsIncluded = false;
        source.Refresh(new[] { first, second });

        // Assert: both occurrences were independently removed.
        Assert.Null(results.Error);
        Assert.Empty(results.RecordedItems);
    }

    /// <summary>
    /// Value-type equality must not replace cache-key identity when refresh re-evaluates a predicate.
    /// </summary>
    [Fact]
    public void EqualValueTypesAreRefreshed_FilterRemovesTheChangedKey()
    {
        // Arrange: the predicate's state can change without replacing either immutable struct.
        using var source = new TestSourceCache<EqualValue, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = new EqualValue(_identityRandomizer.Guid(), equalityValue);
        var second = new EqualValue(_identityRandomizer.Guid(), equalityValue);
        var includedKeys = new HashSet<Guid> { second.Key };

        Assert.NotEqual(first.Key, second.Key);
        Assert.Equal(first, second);

        using var subscription = source.Connect()
            .RemoveKey()
            .Filter(item => includedKeys.Contains(item.Key))
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.AddOrUpdate(new[] { first, second });
        Assert.Null(results.Error);
        Assert.Equal(second.Key, Assert.Single(results.RecordedItems).Key);

        // Act: the previously included key stops matching.
        includedKeys.Remove(second.Key);
        source.Refresh(new[] { second });

        // Assert: the earlier equal struct did not absorb the second key's refresh.
        Assert.Null(results.Error);
        Assert.Empty(results.RecordedItems);
    }

    /// <summary>
    /// Unindexed cache refreshes retain the public self-replacement reason but identify their exact list slot.
    /// Tracking must preserve the original individual additions and their unspecified append indexes.
    /// </summary>
    [Fact]
    public void RefreshWithoutSourceIndex_IdentifiesTheChangedKey()
    {
        // Arrange: both values compare equal, so only their keys identify the refreshed position.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);

        using var subscription = source.Connect()
            .RemoveKey()
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.AddOrUpdate(new[] { first, second });
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
        Assert.Collection(results.RecordedChangeSets[0],
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Same(first, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Same(second, change.Item.Current);
            });

        // Act: refresh the second key without source index metadata.
        source.Refresh(new[] { second });

        // Assert: the original Replace reason is preserved, with both indexes set to the second slot.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
        var replacement = Assert.Single(results.RecordedChangeSets[^1]);
        Assert.Equal(ListChangeReason.Replace, replacement.Reason);
        Assert.Equal(1, replacement.Item.CurrentIndex);
        Assert.Equal(1, replacement.Item.PreviousIndex);
        Assert.True(replacement.Item.Previous.HasValue);
        Assert.Same(second, replacement.Item.Previous.Value);
        Assert.Same(second, replacement.Item.Current);
    }

    /// <summary>
    /// A true update removes the previous value by key and appends the replacement when no index is supplied.
    /// The public remove/add reasons and the untouched equal item's identity must be preserved.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EqualValuesAreUpdated_OnlyTheChangedKeyIsReplaced(bool replacementEqualsPrevious, bool updateFirstItem)
    {
        // Arrange: exercise both a same-position update and an update that appends after the other key.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);
        var previous = updateFirstItem ? first : second;
        var unchanged = updateFirstItem ? second : first;
        var replacement = new EqualItem(
            previous.Key,
            replacementEqualsPrevious ? equalityValue : ~equalityValue,
            isIncluded: true);
        var withoutKeys = source.Connect().RemoveKey();

        using var rawSubscription = withoutKeys
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var rawResults);
        using var subscription = withoutKeys
            .Filter(static item => item.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.AddOrUpdate(new[] { first, second });
        Assert.Null(rawResults.Error);
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));

        // Act: replace one key with a different instance, optionally unequal to its previous value.
        source.AddOrUpdate(replacement);

        // Assert: preserve remove/add semantics and the actual previous and current instances.
        Assert.Null(rawResults.Error);
        Assert.Collection(rawResults.RecordedChangeSets[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Remove, change.Reason);
                Assert.Equal(updateFirstItem ? 0 : 1, change.Item.CurrentIndex);
                Assert.Same(previous, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Same(replacement, change.Item.Current);
            });
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(unchanged, item),
            item => Assert.Same(replacement, item));

        // Act: refresh the replacement at its new position after it stops matching.
        replacement.IsIncluded = false;
        source.Refresh(new[] { replacement });

        // Assert: only the untouched equal item's reference remains.
        Assert.Null(rawResults.Error);
        Assert.Null(results.Error);
        Assert.Same(unchanged, Assert.Single(results.RecordedItems));
    }

    /// <summary>
    /// Removing a cache key removes only its list occurrence; re-addition and a batched clear retain exact membership.
    /// </summary>
    [Fact]
    public void EqualValuesAreRemoved_OnlyTheChangedKeyIsRemoved()
    {
        // Arrange: both equal values are initially included.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);

        using var subscription = source.Connect()
            .RemoveKey()
            .Filter(static item => item.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.AddOrUpdate(new[] { first, second });
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));

        // Act: remove only the second key.
        source.RemoveKey(second.Key);

        // Assert: equal-value lookup must not remove the first reference instead.
        Assert.Null(results.Error);
        Assert.Same(first, Assert.Single(results.RecordedItems));

        // Act: re-add the removed key.
        source.AddOrUpdate(second);

        // Assert: its old position did not leave behind a duplicate or stale entry.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));

        // Act: remove both keys in one changeset.
        source.Clear();

        // Assert: both positions were removed despite their indexes shifting within the batch.
        Assert.Null(results.Error);
        Assert.Empty(results.RecordedItems);
    }

    /// <summary>
    /// Supplied indexed updates and movements must keep key tracking aligned for later unindexed refreshes and removals.
    /// </summary>
    [Fact]
    public void IndexedMovesAndUpdates_KeepEqualValuesAtTheirKeyedPositions()
    {
        // Arrange: sorting supplies indexes for two values that otherwise compare equal.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var items = new[] { CreateEqualItem(equalityValue), CreateEqualItem(equalityValue) }
            .OrderBy(static item => item.Key)
            .ToArray();
        var first = items[0];
        var second = items[1];
        var replacement = new EqualItem(second.Key, equalityValue, isIncluded: true);
        using var comparers = new BehaviorSubject<IComparer<EqualItem>>(
            Comparer<EqualItem>.Create(static (left, right) => left.Key.CompareTo(right.Key)));

        using var subscription = source.Connect()
            .Sort(comparers)
            .RemoveKey()
            .Filter(static item => item.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.AddOrUpdate(items);
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));

        // Act: update the second key without changing its sorted position.
        source.AddOrUpdate(replacement);

        // Assert: the new reference occupies that key's indexed slot.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(replacement, item));

        // Act: reverse the sort order, producing indexed movement.
        comparers.OnNext(Comparer<EqualItem>.Create(static (left, right) => right.Key.CompareTo(left.Key)));

        // Assert: both exact references follow the move.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(replacement, item),
            item => Assert.Same(first, item));

        // Act: refresh the now-second slot after excluding it.
        first.IsIncluded = false;
        source.Refresh(new[] { first });

        // Assert: a correct count is insufficient; the included key's reference must remain.
        Assert.Null(results.Error);
        Assert.Same(replacement, Assert.Single(results.RecordedItems));

        // Act: remove the excluded key, then the included key.
        source.RemoveKey(first.Key);

        // Assert: removing an excluded item does not disturb its equal included neighbor.
        Assert.Null(results.Error);
        Assert.Same(replacement, Assert.Single(results.RecordedItems));

        // Act: remove the final included key after the prior removal shifted its source index.
        source.RemoveKey(replacement.Key);

        // Assert: no item remains at a stale tracked position.
        Assert.Null(results.Error);
        Assert.Empty(results.RecordedItems);
    }

    /// <summary>
    /// Later operations in one changeset use the positions left by earlier removals, without changing operation reasons.
    /// </summary>
    [Fact]
    public void BatchedKeyChanges_UsePositionsAfterEarlierChanges()
    {
        // Arrange: three equal values whose keys identify distinct positions.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);
        var third = CreateEqualItem(equalityValue);
        var replacement = new EqualItem(third.Key, ~equalityValue, isIncluded: true);

        using var subscription = source.Connect()
            .RemoveKey()
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        source.AddOrUpdate(new[] { first, second, third });
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item),
            item => Assert.Same(third, item));

        // Act: remove the leading slot, refresh the next key, and update the final key in one edit.
        source.Edit(updater =>
        {
            updater.Remove(first);
            updater.Refresh(second);
            updater.AddOrUpdate(replacement);
        });

        // Assert: every emitted index describes the list immediately before that operation.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedItems,
            item => Assert.Same(second, item),
            item => Assert.Same(replacement, item));
        Assert.Collection(results.RecordedChangeSets[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Remove, change.Reason);
                Assert.Equal(0, change.Item.CurrentIndex);
                Assert.Same(first, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(0, change.Item.CurrentIndex);
                Assert.Equal(0, change.Item.PreviousIndex);
                Assert.Same(second, change.Item.Previous.Value);
                Assert.Same(second, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Remove, change.Reason);
                Assert.Equal(1, change.Item.CurrentIndex);
                Assert.Same(third, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Same(replacement, change.Item.Current);
            });

        // Act: clear the remaining keys in one edit.
        source.Clear();

        // Assert: retain the original individual removals instead of coalescing them into Clear.
        Assert.Null(results.Error);
        Assert.Empty(results.RecordedItems);
        Assert.Collection(results.RecordedChangeSets[^1],
            change => Assert.Equal(ListChangeReason.Remove, change.Reason),
            change => Assert.Equal(ListChangeReason.Remove, change.Reason));
    }

    /// <summary>
    /// Each subscription to the same RemoveKey observable starts with its own key positions and disposes independently.
    /// </summary>
    [Fact]
    public void SubscriptionsStartedAtDifferentTimes_TrackKeysIndependently()
    {
        // Arrange: the second subscription starts after the first has processed the initial additions.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);
        var withoutKeys = source.Connect().RemoveKey();

        using var firstSubscription = withoutKeys
            .Filter(static item => item.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var firstResults);

        source.AddOrUpdate(new[] { first, second });
        Assert.Null(firstResults.Error);
        Assert.Collection(firstResults.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));

        using var secondSubscription = withoutKeys
            .Filter(static item => item.IsIncluded)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var secondResults);
        Assert.Null(secondResults.Error);
        Assert.Collection(secondResults.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));

        // Act: one source removal must affect both independently tracked subscriptions.
        source.RemoveKey(second.Key);

        // Assert: neither subscription has absorbed the other's initial snapshot or removal.
        Assert.Null(firstResults.Error);
        Assert.Null(secondResults.Error);
        Assert.Same(first, Assert.Single(firstResults.RecordedItems));
        Assert.Same(first, Assert.Single(secondResults.RecordedItems));

        // Act: dispose one subscription, then re-add the key for the remaining subscriber.
        firstSubscription.Dispose();
        source.AddOrUpdate(second);

        // Assert: the disposed observer remains unchanged while the active one receives the addition.
        Assert.Null(firstResults.Error);
        Assert.Null(secondResults.Error);
        Assert.Same(first, Assert.Single(firstResults.RecordedItems));
        Assert.Collection(secondResults.RecordedItems,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    private EqualItem CreateEqualItem(int equalityValue, bool isIncluded = true)
        => new(_identityRandomizer.Guid(), equalityValue, isIncluded);

    // Cache keys are deliberately excluded from value equality. Equal values must still occupy distinct list slots.
    private sealed class EqualItem : IEquatable<EqualItem>
    {
        public EqualItem(Guid key, int equalityValue, bool isIncluded)
        {
            Key = key;
            EqualityValue = equalityValue;
            IsIncluded = isIncluded;
        }

        public Guid Key { get; }

        public int EqualityValue { get; }

        public bool IsIncluded { get; set; }

        public bool Equals(EqualItem? other)
            => other is not null && EqualityValue == other.EqualityValue;

        public override bool Equals(object? obj)
            => obj is EqualItem other && Equals(other);

        public override int GetHashCode()
            => EqualityValue;
    }

    private readonly record struct EqualValue(Guid Key, int EqualityValue)
    {
        public bool Equals(EqualValue other)
            => EqualityValue == other.EqualityValue;

        public override int GetHashCode()
            => EqualityValue;
    }
}
