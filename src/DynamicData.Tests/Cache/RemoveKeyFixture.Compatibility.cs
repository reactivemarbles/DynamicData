using System;
using System.Reactive.Subjects;

using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public partial class RemoveKeyFixture
{
    /// <summary>
    /// Preview is a hot stream without an initial snapshot. Unobserved refreshes, updates, and removals
    /// retain the historical projection and unknown indexes rather than requiring complete cache history.
    /// </summary>
    [Theory]
    [InlineData(ChangeReason.Refresh)]
    [InlineData(ChangeReason.Update)]
    [InlineData(ChangeReason.Remove)]
    public void UnknownKeyChanges_KeepUnspecifiedIndexes(ChangeReason reason)
    {
        // Arrange: subscribe after the original item was added, so its list position is unknown.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var previous = CreateEqualItem(_identityRandomizer.Int());
        var current = new EqualItem(previous.Key, ~previous.EqualityValue, isIncluded: true);
        source.AddOrUpdate(previous);

        // Partial deltas cannot be materialized or validated against an initially empty list.
        using var subscription = source.Preview()
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act: deliver a genuine cache operation without its initial addition.
        switch (reason)
        {
            case ChangeReason.Refresh:
                source.Refresh(new[] { previous });
                break;

            case ChangeReason.Update:
                source.AddOrUpdate(current);
                break;

            case ChangeReason.Remove:
                source.RemoveKey(previous.Key);
                break;
        }

        // Assert: project the original reasons, payloads, and unknown indexes without an error.
        Assert.Null(results.Error);
        var changes = Assert.Single(results.RecordedValues);
        switch (reason)
        {
            case ChangeReason.Refresh:
                var refresh = Assert.Single(changes);
                Assert.Equal(ListChangeReason.Replace, refresh.Reason);
                Assert.Equal(-1, refresh.Item.CurrentIndex);
                Assert.Equal(-1, refresh.Item.PreviousIndex);
                Assert.Same(previous, refresh.Item.Current);
                Assert.Same(previous, refresh.Item.Previous.Value);
                break;

            case ChangeReason.Update:
                Assert.Collection(changes,
                    change =>
                    {
                        Assert.Equal(ListChangeReason.Remove, change.Reason);
                        Assert.Equal(-1, change.Item.CurrentIndex);
                        Assert.Same(previous, change.Item.Current);
                    },
                    change =>
                    {
                        Assert.Equal(ListChangeReason.Add, change.Reason);
                        Assert.Equal(-1, change.Item.CurrentIndex);
                        Assert.Same(current, change.Item.Current);
                    });
                break;

            case ChangeReason.Remove:
                var removal = Assert.Single(changes);
                Assert.Equal(ListChangeReason.Remove, removal.Reason);
                Assert.Equal(-1, removal.Item.CurrentIndex);
                Assert.Same(previous, removal.Item.Current);
                break;
        }

        // Act: complete the source after the partial operation.
        source.Complete();

        // Assert: the operation did not terminate the subscription prematurely.
        Assert.Null(results.Error);
        Assert.True(results.HasCompleted);
    }

    /// <summary>
    /// Supplied indexes can refer to unobserved slots and must pass through without indexing a shorter local list.
    /// Refresh metadata retains its legacy special case: an untracked key still produces an unindexed self-replacement.
    /// </summary>
    [Theory]
    [InlineData(ChangeReason.Add, true, false)]
    [InlineData(ChangeReason.Update, true, true)]
    [InlineData(ChangeReason.Update, true, false)]
    [InlineData(ChangeReason.Update, false, true)]
    [InlineData(ChangeReason.Remove, true, false)]
    [InlineData(ChangeReason.Moved, true, true)]
    [InlineData(ChangeReason.Refresh, true, false)]
    public void UntrackedIndexedChanges_PreserveSuppliedMetadata(ChangeReason reason, bool supplyCurrentIndex, bool supplyPreviousIndex)
    {
        // Arrange: indexes deliberately exceed the subscription's empty observed history.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var previous = CreateEqualItem(_identityRandomizer.Int());
        var current = reason is ChangeReason.Update
            ? new EqualItem(previous.Key, ~previous.EqualityValue, isIncluded: true)
            : previous;
        var previousIndex = supplyPreviousIndex ? _identityRandomizer.Int(3, 30) : -1;
        var currentIndex = supplyCurrentIndex ? _identityRandomizer.Int(31, 60) : -1;
        var input = reason switch
        {
            ChangeReason.Update => new Change<EqualItem, Guid>(reason, current.Key, current, previous, currentIndex, previousIndex),
            ChangeReason.Moved => new Change<EqualItem, Guid>(current.Key, current, currentIndex, previousIndex),
            _ => new Change<EqualItem, Guid>(reason, current.Key, current, currentIndex)
        };

        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act: deliver one indexed change without an initial snapshot.
        source.OnNext(new ChangeSet<EqualItem, Guid> { input });

        // Assert: no range restriction is imposed by the local tracking collection.
        Assert.Null(results.Error);
        var changes = Assert.Single(results.RecordedValues);
        if (reason is ChangeReason.Update)
        {
            Assert.Collection(changes,
                change =>
                {
                    Assert.Equal(ListChangeReason.Remove, change.Reason);
                    Assert.Equal(previousIndex, change.Item.CurrentIndex);
                    Assert.Same(previous, change.Item.Current);
                },
                change =>
                {
                    Assert.Equal(ListChangeReason.Add, change.Reason);
                    Assert.Equal(currentIndex, change.Item.CurrentIndex);
                    Assert.Same(current, change.Item.Current);
                });
        }
        else
        {
            var change = Assert.Single(changes);
            var expectedReason = reason switch
            {
                ChangeReason.Add => ListChangeReason.Add,
                ChangeReason.Remove => ListChangeReason.Remove,
                ChangeReason.Moved => ListChangeReason.Moved,
                _ => ListChangeReason.Replace
            };
            Assert.Equal(expectedReason, change.Reason);
            Assert.Equal(reason is ChangeReason.Refresh ? -1 : currentIndex, change.Item.CurrentIndex);
            Assert.Equal(reason is ChangeReason.Moved ? previousIndex : -1, change.Item.PreviousIndex);
            Assert.Same(current, change.Item.Current);
            if (reason is ChangeReason.Refresh)
                Assert.Same(current, change.Item.Previous.Value);
        }

        if (reason is ChangeReason.Add or ChangeReason.Update or ChangeReason.Moved)
        {
            // Act: refresh the resulting key without supplying a new position.
            source.OnNext(new ChangeSet<EqualItem, Guid>
            {
                new(ChangeReason.Refresh, current.Key, current)
            });

            // Assert: reuse a supplied destination across an unobserved gap; an unspecified destination stays unknown.
            Assert.Null(results.Error);
            var refresh = Assert.Single(results.RecordedValues[^1]);
            Assert.Equal(ListChangeReason.Replace, refresh.Reason);
            Assert.Equal(currentIndex, refresh.Item.CurrentIndex);
            Assert.Equal(currentIndex, refresh.Item.PreviousIndex);
            Assert.Same(current, refresh.Item.Current);
            Assert.Same(current, refresh.Item.Previous.Value);
        }
    }

    /// <summary>
    /// An unknown refresh is nonstructural: it must not discard other keys' known positions.
    /// Its missing history does prevent guessing the destination of a later unindexed append.
    /// </summary>
    [Fact]
    public void UnknownRefresh_PreservesKnownPositionsWithoutGuessingTheAppendIndex()
    {
        // Arrange: two observed keys and an unobserved key all contain equal values.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);
        var unknown = CreateEqualItem(equalityValue);
        var appended = CreateEqualItem(equalityValue);

        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Add, first.Key, first),
            new(ChangeReason.Add, second.Key, second)
        });
        Assert.Null(results.Error);

        // Act: refresh the unknown key, then a known key, without changing any positions.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Refresh, unknown.Key, unknown),
            new(ChangeReason.Refresh, second.Key, second)
        });

        // Assert: only the genuinely untracked key retains unknown indexes.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedValues[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Equal(-1, change.Item.PreviousIndex);
                Assert.Same(unknown, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(1, change.Item.CurrentIndex);
                Assert.Equal(1, change.Item.PreviousIndex);
                Assert.Same(second, change.Item.Current);
            });

        // Act: append without an index, after discovering that some source contents were never observed.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Add, appended.Key, appended),
            new(ChangeReason.Refresh, appended.Key, appended),
            new(ChangeReason.Refresh, second.Key, second)
        });

        // Assert: do not fabricate an end position, and do not lose the previously known second slot.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedValues[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Same(appended, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Same(appended, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(1, change.Item.CurrentIndex);
                Assert.Same(second, change.Item.Current);
            });
    }

    /// <summary>
    /// An unknown removal position cannot safely shift other tracked positions. Preserve projection, discard
    /// uncertain inference, and allow later supplied indexes to establish positions again.
    /// </summary>
    [Theory]
    [InlineData(ChangeReason.Remove)]
    [InlineData(ChangeReason.Update)]
    public void UnknownStructuralChanges_InvalidateUncertainPositionsAndAllowIndexedRecovery(ChangeReason reason)
    {
        // Arrange: an equal but untracked key can have occupied an unknown position before the known key.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var equalityValue = _identityRandomizer.Int();
        var known = CreateEqualItem(equalityValue);
        var unknown = CreateEqualItem(equalityValue);
        var replacement = new EqualItem(unknown.Key, ~equalityValue, isIncluded: true);
        var recovered = CreateEqualItem(equalityValue);
        var recoveredIndex = _identityRandomizer.Int(3, 30);

        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);
        source.OnNext(new ChangeSet<EqualItem, Guid> { new(ChangeReason.Add, known.Key, known) });
        Assert.Null(results.Error);

        // Act: change an untracked key without supplying its previous position.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            reason is ChangeReason.Update
                ? new Change<EqualItem, Guid>(reason, unknown.Key, replacement, unknown)
                : new Change<EqualItem, Guid>(reason, unknown.Key, unknown)
        });

        // Assert: the partial operation still projects its exact changes with unspecified positions.
        Assert.Null(results.Error);
        if (reason is ChangeReason.Update)
        {
            Assert.Collection(results.RecordedValues[^1],
                change =>
                {
                    Assert.Equal(ListChangeReason.Remove, change.Reason);
                    Assert.Equal(-1, change.Item.CurrentIndex);
                    Assert.Same(unknown, change.Item.Current);
                },
                change =>
                {
                    Assert.Equal(ListChangeReason.Add, change.Reason);
                    Assert.Equal(-1, change.Item.CurrentIndex);
                    Assert.Same(replacement, change.Item.Current);
                });
        }
        else
        {
            var removal = Assert.Single(results.RecordedValues[^1]);
            Assert.Equal(ListChangeReason.Remove, removal.Reason);
            Assert.Equal(-1, removal.Item.CurrentIndex);
            Assert.Same(unknown, removal.Item.Current);
        }

        // Act: refresh the formerly known key, then provide an explicit position for a new key.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Refresh, known.Key, known),
            new(ChangeReason.Add, recovered.Key, recovered, recoveredIndex),
            new(ChangeReason.Refresh, recovered.Key, recovered)
        });

        // Assert: uncertain positions remain unknown, while the supplied position can be reused safely.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedValues[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Same(known, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(recoveredIndex, change.Item.CurrentIndex);
                Assert.Same(recovered, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(recoveredIndex, change.Item.CurrentIndex);
                Assert.Equal(recoveredIndex, change.Item.PreviousIndex);
                Assert.Same(recovered, change.Item.Current);
            });
    }

    /// <summary>
    /// A supplied removal index in an unobserved gap shifts only known positions after that index.
    /// Tracking must neither allocate the gap nor remove an equal-valued known neighbor instead.
    /// </summary>
    [Fact]
    public void UnknownIndexedRemoval_ShiftsKnownSparsePositions()
    {
        // Arrange: observe two indexed additions with an unobserved gap between them.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);
        var unknown = CreateEqualItem(equalityValue);
        var firstIndex = _identityRandomizer.Int(3, 30);
        var removalIndex = firstIndex + _identityRandomizer.Int(1, 30);
        var secondIndex = removalIndex + _identityRandomizer.Int(1, 30);

        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Add, first.Key, first, firstIndex),
            new(ChangeReason.Add, second.Key, second, secondIndex)
        });
        Assert.Null(results.Error);

        // Act: remove an unobserved key from the gap, then refresh both known keys.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Remove, unknown.Key, unknown, removalIndex),
            new(ChangeReason.Refresh, first.Key, first),
            new(ChangeReason.Refresh, second.Key, second)
        });

        // Assert: preserve the supplied removal and adjust only the subsequent known position.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedValues[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Remove, change.Reason);
                Assert.Equal(removalIndex, change.Item.CurrentIndex);
                Assert.Same(unknown, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(firstIndex, change.Item.CurrentIndex);
                Assert.Equal(firstIndex, change.Item.PreviousIndex);
                Assert.Same(first, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(secondIndex - 1, change.Item.CurrentIndex);
                Assert.Equal(secondIndex - 1, change.Item.PreviousIndex);
                Assert.Same(second, change.Item.Current);
            });
    }

    /// <summary>
    /// A supplied index that contradicts observed history remains authoritative for projection.
    /// Uncertain inferred positions must be discarded rather than used to address another equal item.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingSuppliedIndexes_DoNotInventSubsequentPositions(bool removeKnownKey)
    {
        // Arrange: inferred positions are inconsistent with a later supplied removal position.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);
        var removed = removeKnownKey ? second : CreateEqualItem(equalityValue);

        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Add, first.Key, first),
            new(ChangeReason.Add, second.Key, second)
        });
        Assert.Null(results.Error);

        // Act: pass through the conflicting supplied index, then refresh an untouched key.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Remove, removed.Key, removed, 0),
            new(ChangeReason.Refresh, first.Key, first)
        });

        // Assert: preserve the original removal metadata without pretending the other key's position is still known.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedValues[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Remove, change.Reason);
                Assert.Equal(0, change.Item.CurrentIndex);
                Assert.Same(removed, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Equal(-1, change.Item.PreviousIndex);
                Assert.Same(first, change.Item.Current);
            });
    }

    /// <summary>
    /// Projection accepts boundary index metadata without allocating missing list contents.
    /// An inferred position beyond the index type's range must become unknown instead of wrapping.
    /// </summary>
    [Fact]
    public void BoundaryIndexMetadata_DoesNotAllocateGapsOrWrapTrackedPositions()
    {
        // Arrange: values and keys are generated; int.MaxValue exercises the positional metadata boundary.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);

        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act: inserting before the furthest representable position makes that older position unrepresentable.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Add, first.Key, first, int.MaxValue),
            new(ChangeReason.Add, second.Key, second, 0),
            new(ChangeReason.Refresh, first.Key, first),
            new(ChangeReason.Refresh, second.Key, second)
        });

        // Assert: original indexes pass through and only still-representable known positions are inferred.
        Assert.Null(results.Error);
        Assert.Collection(Assert.Single(results.RecordedValues),
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(int.MaxValue, change.Item.CurrentIndex);
                Assert.Same(first, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(0, change.Item.CurrentIndex);
                Assert.Same(second, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(-1, change.Item.CurrentIndex);
                Assert.Equal(-1, change.Item.PreviousIndex);
                Assert.Same(first, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(0, change.Item.CurrentIndex);
                Assert.Equal(0, change.Item.PreviousIndex);
                Assert.Same(second, change.Item.Current);
            });
    }
}
