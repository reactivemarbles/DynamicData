using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Bogus;
using Xunit;

using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public partial class RemoveKeyFixture
{
    private const int PartialStreamSeed = 0x1192;

    private readonly Randomizer _partialStreamRandomizer = new(PartialStreamSeed);

    /// <summary>
    /// A <see cref="ObservableCacheEx.Preview{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/> subscription
    /// taken after the existing items were added omits part of its history, so it is outside the operator's contract.
    /// No claim is made about where its entries land relative to items it never observed, because the stream carries
    /// nothing that could establish that. What must still hold is that the operator only ever addresses entries it
    /// established itself, so a removal cannot delete an item this subscription never saw.
    /// </summary>
    [Fact]
    public void OutOfContractStreamIntoPopulatedList_DoesNotRemoveTheUnobservedItem()
    {
        // Arrange: both the cache and the target list already hold an item this subscription will never observe.
        var existing = _partialStreamRandomizer.Int();
        var added = _partialStreamRandomizer.Int();
        Assert.NotEqual(existing, added);

        using var source = new TestSourceCache<int, int>(static value => value);
        source.AddOrUpdate(existing);

        var target = new List<int> { existing };

        using var subscription = source.Preview()
            .RemoveKey()
            .ValidateSynchronization()
            .Clone(target)
            .Subscribe();

        // Act: the only changes this subscription observes are an add and the matching remove.
        // Where the addition lands relative to the unobserved entry is deliberately not asserted.
        source.AddOrUpdate(added);
        Assert.Contains(existing, target);

        source.RemoveKey(added);

        // Assert: the unobserved item must survive; only the observed item may be removed.
        Assert.Equal(new[] { existing }, target);
    }

    /// <summary>
    /// The same contract holds when the observed additions outnumber the unobserved remainder, and when the
    /// removals arrive in an order other than the order the additions were observed in.
    /// </summary>
    [Fact]
    public void PartialStreamIntoPopulatedList_RemovesObservedItemsInAnyOrder()
    {
        // Arrange: a pre-populated target the subscription cannot see.
        var existing = _partialStreamRandomizer.Int();
        var observed = Enumerable.Range(0, 4).Select(_ => _partialStreamRandomizer.Int()).Distinct().ToArray();
        Assert.DoesNotContain(existing, observed);

        using var source = new TestSourceCache<int, int>(static value => value);
        source.AddOrUpdate(existing);

        var target = new List<int> { existing };

        using var subscription = source.Preview()
            .RemoveKey()
            .ValidateSynchronization()
            .Clone(target)
            .Subscribe();

        // Act: add every observed value, then remove them from the middle outwards.
        source.AddOrUpdate(observed);
        foreach (var value in new[] { observed[2], observed[0], observed[3], observed[1] })
            source.RemoveKey(value);

        // Assert: the unobserved item is the only survivor.
        Assert.Equal(new[] { existing }, target);
    }

    /// <summary>
    /// Clearing a cache removes every key in a single changeset. Position maintenance must not rewrite the surviving
    /// suffix for each individual removal, otherwise an ordinary collection size costs a quadratic number of updates.
    /// The emitted changes must stay individual removals carrying their sequential positions. The bound below is a
    /// regression guard against the quadratic behaviour; it does not by itself establish a growth rate.
    /// </summary>
    [Theory]
    [InlineData(50_000)]
    public void BulkRemovalCompletesWithoutQuadraticCost(int count)
    {
        // Arrange: a populated cache observed through an otherwise inert subscription.
        using var source = new TestSourceCache<int, int>(static value => value);
        source.AddOrUpdate(Enumerable.Range(0, count));

        var removedCount = 0;
        using var subscription = source.Connect()
            .RemoveKey()
            .ValidateSynchronization()
            .Subscribe(changes => removedCount += changes.Count(static change => change.Reason == ListChangeReason.Remove));

        // Act: a single synchronous clear of every entry, in insertion order.
        var stopwatch = Stopwatch.StartNew();
        source.Clear();
        stopwatch.Stop();

        // Assert: every entry is reported individually and the clear does not block for a quadratic interval.
        Assert.Equal(count, removedCount);
        Assert.Empty(source.Items);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Clearing {count} entries took {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// Removal cost must not depend on how much of the collection survives the removal. Removing from the front,
    /// where the entire remainder would need reindexing, must cost the same order as removing from the back.
    /// </summary>
    [Fact]
    public void FrontAndBackRemovalCostTheSameOrder()
    {
        // Arrange: two equally sized caches observed through equivalent subscriptions.
        const int Count = 50_000;

        var fromFront = MeasureSequentialRemoval(Count, ascending: true);
        var fromBack = MeasureSequentialRemoval(Count, ascending: false);

        // Assert: neither direction degrades into a suffix rewrite per removal.
        Assert.True(fromFront < TimeSpan.FromSeconds(3), $"Front removal took {fromFront.TotalMilliseconds} ms.");
        Assert.True(fromBack < TimeSpan.FromSeconds(3), $"Back removal took {fromBack.TotalMilliseconds} ms.");
    }

    /// <summary>
    /// Removal cost has to stay visible in a mixed workload. A benchmark that only drains a collection, or one
    /// dominated by refreshes and additions, can hide a slow removal path, which is the specific risk of tuning the
    /// key lookup on its own. This interleaves removals of randomly chosen surviving keys with additions, updates,
    /// and refreshes, so each removal happens in the middle of a collection that stays large rather than only while
    /// it is draining. The bound below is a regression guard against quadratic behaviour; it does not by itself
    /// establish a growth rate.
    /// </summary>
    [Theory]
    [InlineData(20_000)]
    public void MixedRemovalWorkloadCompletesWithoutQuadraticCost(int count)
    {
        // Arrange: a populated cache observed through an otherwise inert subscription. The key type differs from the
        // item type so that the refresh overload resolves without ambiguity.
        var randomizer = new Randomizer(PartialStreamSeed);
        using var source = new TestSourceCache<int, long>(static value => value);
        source.AddOrUpdate(Enumerable.Range(0, count));

        var removals = 0;
        using var subscription = source.Connect()
            .RemoveKey()
            .ValidateSynchronization()
            .Subscribe(changes => removals += changes.Count(static change => change.Reason == ListChangeReason.Remove));

        // Shuffle the removal order so no removal is at a cheap end of the collection.
        var order = Enumerable.Range(0, count).ToList();
        for (var index = order.Count - 1; index > 0; --index)
        {
            var swap = randomizer.Int(0, index);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        var next = count;
        var expectedRemovals = 0;

        // Act: interleave removals with the operations that share the same position lookup.
        var stopwatch = Stopwatch.StartNew();
        foreach (var value in order)
        {
            source.RemoveKey(value);
            ++expectedRemovals;

            // Keep the collection large so the following removals cannot get cheaper as the run proceeds.
            var added = next++;
            source.AddOrUpdate(added);

            if (value % 3 == 0)
            {
                // An update is a remove-then-add pair, and a refresh exercises the lookup without a structural edit.
                source.AddOrUpdate(added);
                ++expectedRemovals;
                source.Refresh(new[] { added });
            }
        }

        stopwatch.Stop();

        // Assert: every removal is reported individually, the collection is intact, and the run does not block for a
        // quadratic interval.
        Assert.Equal(expectedRemovals, removals);
        Assert.Equal(count, source.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Mixed workload over {count} entries took {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// The positions the operator reports must describe the list a consumer actually builds from them. Replaying a
    /// randomized sequence of additions, updates, and removals into a cloned list must therefore agree exactly with
    /// the same sequence applied directly to a plain list, for every intermediate state and not only the final one.
    /// </summary>
    [Theory]
    [InlineData(2_000)]
    public void RandomizedEditsAgreeWithAPlainListOracle(int operations)
    {
        // Arrange: a complete stream from an empty cache, cloned into the list under comparison.
        var randomizer = new Randomizer(PartialStreamSeed);
        using var source = new TestSourceCache<int, int>(static value => value);

        var target = new List<int>();
        var expected = new List<int>();

        using var subscription = source.Connect()
            .RemoveKey()
            .ValidateSynchronization()
            .ValidateChangeSets()
            .Clone(target)
            .Subscribe();

        // Act: apply each operation to the cache and to the oracle, comparing after every edit.
        for (var operation = 0; operation < operations; ++operation)
        {
            var value = randomizer.Int(0, 400);
            var present = expected.Contains(value);

            switch (randomizer.Int(0, 2))
            {
                // Add a new key, or update an existing one, which is a removal followed by an append.
                case 0:
                case 1:
                    source.AddOrUpdate(value);
                    if (present)
                        expected.Remove(value);

                    expected.Add(value);
                    break;

                case 2:
                    source.RemoveKey(value);
                    if (present)
                        expected.Remove(value);

                    break;
            }

            // Assert: the cloned list matches the oracle at every step, so no index was ever misreported.
            Assert.Equal(expected, target);
        }
    }

    private static TimeSpan MeasureSequentialRemoval(int count, bool ascending)
    {
        using var source = new TestSourceCache<int, int>(static value => value);
        source.AddOrUpdate(Enumerable.Range(0, count));

        using var subscription = source.Connect().RemoveKey().Subscribe();

        var order = ascending ? Enumerable.Range(0, count) : Enumerable.Range(0, count).Reverse();

        var stopwatch = Stopwatch.StartNew();
        source.Edit(updater =>
        {
            foreach (var value in order)
                updater.RemoveKey(value);
        });
        stopwatch.Stop();

        return stopwatch.Elapsed;
    }
}
