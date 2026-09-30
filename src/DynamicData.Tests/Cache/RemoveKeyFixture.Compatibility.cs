using System;
using System.Reactive.Subjects;

using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public partial class RemoveKeyFixture
{
    /// <summary>
    /// The operator's contract is a complete history applied to an initially empty list. A stream that omits part of
    /// that history, such as a <see cref="ObservableCacheEx.Preview{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// subscription taken after the additions, is outside the contract. No position can be recovered from information
    /// the stream never carried, so the guarantee is only that such a change is projected with its original reason and
    /// payload, reports an unknown position where none is known, and never faults the subscription.
    /// </summary>
    [Theory]
    [InlineData(ChangeReason.Refresh)]
    [InlineData(ChangeReason.Update)]
    [InlineData(ChangeReason.Remove)]
    public void OutOfContractChanges_ProjectReasonsWithoutFaulting(ChangeReason reason)
    {
        // Arrange: subscribe after the original item was added, so its position was never observed.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var previous = CreateEqualItem(_identityRandomizer.Int());
        var current = new EqualItem(previous.Key, ~previous.EqualityValue, isIncluded: true);
        source.AddOrUpdate(previous);

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

        // Assert: reasons and payloads survive, and a position that was never observed is reported as unknown.
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
                        // The replacement is the first entry this subscription has seen, so it takes the only
                        // position the operator's own list has. That is not an attempt to reconstruct the history.
                        Assert.Equal(ListChangeReason.Add, change.Reason);
                        Assert.Equal(0, change.Item.CurrentIndex);
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

        // Act: complete the source after the out-of-contract operation.
        source.Complete();

        // Assert: the operation did not terminate the subscription prematurely.
        Assert.Null(results.Error);
        Assert.True(results.HasCompleted);
    }

    /// <summary>
    /// A repeated addition of a key that is already present cannot occur under the contract, because the cache
    /// reports a second arrival as an update. It must still not corrupt the positions of the entries around it or
    /// fault the subscription.
    /// </summary>
    [Fact]
    public void OutOfContractDuplicateAddition_LeavesSurroundingPositionsIntact()
    {
        // Arrange: two observed entries holding equal values.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);

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

        // Act: re-add a key that is already tracked, then remove the other key.
        source.OnNext(new ChangeSet<EqualItem, Guid> { new(ChangeReason.Add, first.Key, first) });
        source.OnNext(new ChangeSet<EqualItem, Guid> { new(ChangeReason.Remove, second.Key, second) });

        // Assert: the duplicate reports an unknown position and the surviving entry keeps the position it held.
        Assert.Null(results.Error);
        var duplicate = Assert.Single(results.RecordedValues[1]);
        Assert.Equal(ListChangeReason.Add, duplicate.Reason);
        Assert.Equal(-1, duplicate.Item.CurrentIndex);

        var removal = Assert.Single(results.RecordedValues[^1]);
        Assert.Equal(ListChangeReason.Remove, removal.Reason);
        Assert.Equal(1, removal.Item.CurrentIndex);
        Assert.Same(second, removal.Item.Current);
    }

    /// <summary>
    /// A refresh is nonstructural, so a refresh for a key this subscription never observed must not discard the
    /// positions of the keys it did observe, and must not prevent a later unindexed append from reporting the slot
    /// it actually occupies in this subscription's list.
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

        // Act: append without an index, after an out-of-contract refresh has already been seen.
        source.OnNext(new ChangeSet<EqualItem, Guid>
        {
            new(ChangeReason.Add, appended.Key, appended),
            new(ChangeReason.Refresh, appended.Key, appended),
            new(ChangeReason.Refresh, second.Key, second)
        });

        // Assert: the append lands at the end of the list this subscription has built, and that slot is
        // immediately reportable. The unobserved source entries never occupied a slot here to begin with.
        Assert.Null(results.Error);
        Assert.Collection(results.RecordedValues[^1],
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(2, change.Item.CurrentIndex);
                Assert.Same(appended, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Replace, change.Reason);
                Assert.Equal(2, change.Item.CurrentIndex);
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
    /// Position tracking is created per subscription, inside the deferred body, so the terminal notifications have to
    /// keep travelling through it untouched. A fault that arrives after positions were established must reach the
    /// subscriber as the same exception, without being converted into a completion or held back by the tracked state.
    /// </summary>
    [Fact]
    public void SourceError_IsForwardedAfterTheChangesThatPrecededIt()
    {
        // Arrange: establish tracked positions before the source faults.
        using var source = new TestSourceCache<EqualItem, Guid>(static item => item.Key);
        var equalityValue = _identityRandomizer.Int();
        var first = CreateEqualItem(equalityValue);
        var second = CreateEqualItem(equalityValue);
        var expected = new InvalidOperationException("The source faulted after positions were tracked.");

        using var subscription = source.Connect()
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);
        source.AddOrUpdate(new[] { first, second });
        Assert.Null(results.Error);

        // Act: fault the source.
        source.SetError(expected);

        // Assert: the changes observed before the fault stand, and the fault itself is forwarded verbatim.
        Assert.Same(expected, results.Error);
        Assert.False(results.HasCompleted);
        Assert.Collection(Assert.Single(results.RecordedValues),
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(0, change.Item.CurrentIndex);
                Assert.Same(first, change.Item.Current);
            },
            change =>
            {
                Assert.Equal(ListChangeReason.Add, change.Reason);
                Assert.Equal(1, change.Item.CurrentIndex);
                Assert.Same(second, change.Item.Current);
            });
    }

    /// <summary>
    /// The deferred body runs at subscription time, so a source that already completed must complete the subscriber
    /// immediately rather than leaving it waiting on a stream that will never emit.
    /// </summary>
    [Fact]
    public void SourceThatCompletedBeforeSubscription_CompletesWithoutEmitting()
    {
        // Arrange: the source reaches its terminal state before anything subscribes.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        source.OnCompleted();

        // Act: subscribe to the already terminated source.
        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Assert: completion is passed straight through and no changeset is invented for the empty list.
        Assert.Null(results.Error);
        Assert.True(results.HasCompleted);
        Assert.Empty(results.RecordedValues);
    }

    /// <summary>
    /// The error counterpart of subscribing to an already terminated source. Creating the per subscription position
    /// state must not swallow or replace an error that is already waiting.
    /// </summary>
    [Fact]
    public void SourceThatErroredBeforeSubscription_ForwardsTheErrorWithoutEmitting()
    {
        // Arrange: the source faults before anything subscribes.
        using var source = new Subject<IChangeSet<EqualItem, Guid>>();
        var expected = new InvalidOperationException("The source faulted before the subscription was taken.");
        source.OnError(expected);

        // Act: subscribe to the already faulted source.
        using var subscription = source
            .RemoveKey()
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Assert: the original exception instance arrives, and it is not reported as a completion.
        Assert.Same(expected, results.Error);
        Assert.False(results.HasCompleted);
        Assert.Empty(results.RecordedValues);
    }
}
