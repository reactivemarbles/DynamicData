#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Internal;

public class CacheInfrastructureCoverageFixture
{
    [Test]
    public async Task LockFreeObservableCacheConnectEmitsInitialSnapshotAndLiveUpdates()
    {
        using var cache = new LockFreeObservableCache<Person, string>();
        var first = new Person("One", 1);
        var second = new Person("Two", 2);

        cache.Edit(updater => updater.AddOrUpdate(first, first.Name));

        using var results = cache.Connect(suppressEmptyChangeSets: false).AsAggregator();

        cache.Edit(updater => updater.AddOrUpdate(second, second.Name));

        await Assert.That(cache.Count).IsEqualTo(2);
        await Assert.That(cache.Items).IsEquivalentTo(new[] { first, second });
        await Assert.That(cache.Keys).IsEquivalentTo(new[] { first.Name, second.Name });
        await Assert.That(cache.KeyValues[first.Name]).IsSameReferenceAs(first);
        await Assert.That(cache.Lookup(second.Name).Value).IsSameReferenceAs(second);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].Single().Current).IsSameReferenceAs(first);
        await Assert.That(results.Messages[1].Single().Current).IsSameReferenceAs(second);
    }

    [Test]
    public async Task LockFreeObservableCacheConnectHonorsPredicateAndEmptySuppression()
    {
        using var cache = new LockFreeObservableCache<Person, string>();
        var included = new Person("Adult", 30);
        var excluded = new Person("Child", 10);

        cache.Edit(updater => updater.AddOrUpdate(new[]
        {
            new KeyValuePair<string, Person>(included.Name, included),
            new KeyValuePair<string, Person>(excluded.Name, excluded),
        }));

        using var suppressed = cache.Connect(static person => person.Age >= 18).AsAggregator();
        using var unsuppressed = cache.Connect(static person => person.Age > 100, suppressEmptyChangeSets: false).AsAggregator();

        cache.Edit(updater => updater.AddOrUpdate(new Person("StillChild", 12), "StillChild"));

        await Assert.That(suppressed.Data.Count).IsEqualTo(1);
        await Assert.That(suppressed.Data.Lookup(included.Name).Value).IsSameReferenceAs(included);
        await Assert.That(suppressed.Messages.Count).IsEqualTo(1).Because("non-matching live updates should be suppressed");
        await Assert.That(unsuppressed.Messages.Count).IsEqualTo(2).Because("suppressEmptyChangeSets: false should surface each empty initial/live evaluation");
        await Assert.That(unsuppressed.Messages.All(static message => message.Count == 0)).IsTrue();
    }

    [Test]
    public async Task LockFreeObservableCacheCountChangedStartsWithCurrentCountAndCompletesOnDispose()
    {
        using var cache = new LockFreeObservableCache<Person, string>();
        cache.Edit(updater => updater.AddOrUpdate(new Person("Existing", 1), "Existing"));

        var counts = new List<int>();
        var completed = false;
        using var subscription = cache.CountChanged.Subscribe(counts.Add, _ => { }, () => completed = true);

        cache.Dispose();

        await Assert.That(counts).IsEquivalentTo(new[] { 1 });
        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task LockFreeObservableCacheWatchEmitsInitialAndMatchingLiveChangesOnly()
    {
        using var cache = new LockFreeObservableCache<Person, string>();
        var initial = new Person("Tracked", 1);
        var updated = new Person("Tracked", 2);

        cache.Edit(updater => updater.AddOrUpdate(initial, initial.Name));

        var watched = new List<Change<Person, string>>();
        using var subscription = cache.Watch(initial.Name).Subscribe(watched.Add);

        cache.Edit(
            updater =>
            {
                updater.AddOrUpdate(new Person("Other", 3), "Other");
                updater.AddOrUpdate(updated, updated.Name);
                updater.Remove("Other");
            });

        await Assert.That(watched.Select(static change => change.Reason)).IsEquivalentTo(new[] { ChangeReason.Add, ChangeReason.Update }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(watched[0].Current).IsSameReferenceAs(initial);
        await Assert.That(watched[1].Current).IsSameReferenceAs(updated);
    }

    [Test]
    public async Task LockFreeObservableCacheConstructedFromSourceClonesChangesAndCompletesOnDispose()
    {
        using var source = new SourceCache<Person, string>(static person => person.Name);
        using var cache = new LockFreeObservableCache<Person, string>(source.Connect());
        var person = new Person("Source", 1);

        using var results = cache.Connect().AsAggregator();

        source.AddOrUpdate(person);

        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(results.Data.Lookup(person.Name).Value).IsSameReferenceAs(person);

        cache.Dispose();

        await Assert.That(results.IsCompleted).IsTrue();
    }

    [Test]
    public async Task CacheUpdaterWithDictionaryExposesKeyValuesAndLoadsReplacementItems()
    {
        var original = new Person("Original", 1);
        var replacement = new Person("Replacement", 2);
        var updater = new CacheUpdater<Person, string>(new Dictionary<string, Person> { [original.Name] = original }, static person => person.Name);

        updater.Load(new[] { replacement });

        await Assert.That(updater.Count).IsEqualTo(1);
        await Assert.That(updater.Items).IsEquivalentTo(new[] { replacement });
        await Assert.That(updater.Keys).IsEquivalentTo(new[] { replacement.Name });
        await Assert.That(updater.KeyValues.Single().Value).IsSameReferenceAs(replacement);
        await Assert.That(updater.GetKey(replacement)).IsEqualTo(replacement.Name);
        await Assert.That(updater.GetKeyValues(new[] { replacement }).Single().Key).IsEqualTo(replacement.Name);
        await Assert.That(updater.Lookup(replacement).Value).IsSameReferenceAs(replacement);
    }

    [Test]
    public async Task CacheUpdaterComparerOverloadsSkipEquivalentUpdatesAndApplyDifferentItems()
    {
        var cache = new ChangeAwareCache<Person, string>();
        var updater = new CacheUpdater<Person, string>(cache, static person => person.Name);
        var original = new Person("SameAge", 42);
        var sameAge = new Person("SameAge", 42);
        var newAge = new Person("SameAge", 43);

        updater.AddOrUpdate(original);
        updater.AddOrUpdate(sameAge, Person.AgeComparer);
        updater.AddOrUpdate(new[] { newAge }, Person.AgeComparer);

        var changes = cache.CaptureChanges();

        await Assert.That(cache.Lookup(original.Name).Value).IsSameReferenceAs(newAge);
        await Assert.That(changes.Count).IsEqualTo(2);
        await Assert.That(changes.Select(static change => change.Reason)).IsEquivalentTo(new[] { ChangeReason.Add, ChangeReason.Update }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task CacheUpdaterRefreshAndRemoveEnumerableOverloadsUseSelectedKeys()
    {
        var cache = new ChangeAwareCache<Person, string>();
        var updater = new CacheUpdater<Person, string>(cache, static person => person.Name);
        var first = new Person("First", 1);
        var second = new Person("Second", 2);
        var third = new Person("Third", 3);

        updater.AddOrUpdate(new[] { first, second, third });
        cache.CaptureChanges();

        updater.Refresh(new[] { first, second });
        updater.Refresh(new[] { third.Name });
        updater.Remove(new[] { first });
        updater.Remove(new[] { second.Name });
        updater.Remove(new[] { new KeyValuePair<string, Person>(third.Name, third) });

        var changes = cache.CaptureChanges();

        await Assert.That(cache.Count).IsEqualTo(0);
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Refresh)).IsEqualTo(3);
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Remove)).IsEqualTo(3);
    }

    [Test]
    public async Task CacheUpdaterThrowsWhenKeySelectorIsRequiredButMissing()
    {
        var updater = new CacheUpdater<Person, string>(new ChangeAwareCache<Person, string>());
        var person = new Person("MissingSelector", 1);

        await Assert.That(() => updater.AddOrUpdate(person)).Throws<KeySelectorException>();
        await Assert.That(() => updater.AddOrUpdate(new[] { person })).Throws<KeySelectorException>();
        await Assert.That(() => updater.GetKey(person)).Throws<KeySelectorException>();
        await Assert.That(() => updater.Lookup(person)).Throws<KeySelectorException>();
        await Assert.That(() => updater.Refresh(person)).Throws<KeySelectorException>();
        await Assert.That(() => updater.Remove(person)).Throws<KeySelectorException>();
    }
}
