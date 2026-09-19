#if REACTIVE_TESTS
using DynamicData.Reactive;
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif

namespace DynamicData.Tests.Cache;

public class CacheLifecycleCoverageFixture
{
    [Test]
    public async Task IntermediateCacheDefersConnectSubscribedDuringEditUntilEditCompletes()
    {
        using var cache = new IntermediateCache<CacheItem, string>();
        var messages = new List<IChangeSet<CacheItem, string>>();
        IDisposable? subscription = null;
        var item = new CacheItem("alpha", 1);
        var messagesDuringEdit = -1;

        try
        {
            cache.Edit(
                updater =>
                {
                    updater.AddOrUpdate(item, item.Key);
                    subscription = cache.Connect(suppressEmptyChangeSets: false).Subscribe(messages.Add);
                    messagesDuringEdit = messages.Count;
                });

            await Assert.That(messagesDuringEdit).IsEqualTo(0);
            await Assert.That(messages.Count).IsEqualTo(1);
            await Assert.That(messages[0].Single().Current).IsEqualTo(item);
        }
        finally
        {
            subscription?.Dispose();
        }
    }

    [Test]
    public async Task IntermediateCacheDefersConnectSubscribedWhileNotificationsAndEditAreSuspended()
    {
        using var cache = new IntermediateCache<CacheItem, string>();
        using var suspension = cache.SuspendNotifications();
        var messages = new List<IChangeSet<CacheItem, string>>();
        IDisposable? subscription = null;
        var item = new CacheItem("bravo", 2);
        var messagesDuringEdit = -1;

        try
        {
            cache.Edit(
                updater =>
                {
                    updater.AddOrUpdate(item, item.Key);
                    subscription = cache.Connect(suppressEmptyChangeSets: false).Subscribe(messages.Add);
                    messagesDuringEdit = messages.Count;
                });

            await Assert.That(messagesDuringEdit).IsEqualTo(0);
            await Assert.That(messages).IsEmpty();
        }
        finally
        {
            suspension.Dispose();
        }

        try
        {
            await Assert.That(messages.Count).IsEqualTo(1);
            await Assert.That(messages[0].Single().Current).IsEqualTo(item);
        }
        finally
        {
            subscription?.Dispose();
        }
    }

    [Test]
    public async Task IntermediateCacheCountPreviewWatchAndInitialUpdatesReflectEditedItems()
    {
        using var cache = new IntermediateCache<CacheItem, string>();
        var counts = new List<int>();
        var previews = new List<IChangeSet<CacheItem, string>>();
        var watched = new List<Change<CacheItem, string>>();
        using var countSubscription = cache.CountChanged.Subscribe(counts.Add);
        using var previewSubscription = cache.Preview(static item => item.Value > 10).Subscribe(previews.Add);
        using var watchSubscription = cache.Watch("charlie").Subscribe(watched.Add);
        using var countSuspension = cache.SuspendCount();
        var included = new CacheItem("charlie", 11);
        var excluded = new CacheItem("delta", 4);

        cache.Edit(
            updater =>
            {
                updater.AddOrUpdate(included, included.Key);
                updater.AddOrUpdate(excluded, excluded.Key);
            });

        await Assert.That(cache.Keys).IsEquivalentTo(new[] { included.Key, excluded.Key });
        await Assert.That(cache.KeyValues[included.Key]).IsEqualTo(included);
        await Assert.That(cache.Lookup(included.Key).Value).IsEqualTo(included);
        await Assert.That(cache.GetInitialUpdates(static item => item.Value > 10).Single().Current).IsEqualTo(included);
        await Assert.That(previews.Single().Single().Current).IsEqualTo(included);
        await Assert.That(watched.Single().Current).IsEqualTo(included);
        await Assert.That(counts).IsEquivalentTo(new[] { 0 });
    }

    [Test]
    public async Task AnonymousObservableCacheDelegatesKeyValuesPreviewAndWatch()
    {
        using var source = new SourceCache<CacheItem, string>(static item => item.Key);
        using var anonymous = new AnonymousObservableCache<CacheItem, string>(source.Connect());
        var previews = new List<IChangeSet<CacheItem, string>>();
        var watched = new List<Change<CacheItem, string>>();
        using var previewSubscription = anonymous.Preview(static item => item.Value > 10).Subscribe(previews.Add);
        using var watchSubscription = anonymous.Watch("echo").Subscribe(watched.Add);
        using var connectResults = anonymous.Connect().AsAggregator();
        var included = new CacheItem("echo", 12);
        var excluded = new CacheItem("foxtrot", 3);

        source.AddOrUpdate(included);
        source.AddOrUpdate(excluded);

        await Assert.That(anonymous.KeyValues[included.Key]).IsEqualTo(included);
        await Assert.That(previews.Single().Single().Current).IsEqualTo(included);
        await Assert.That(watched.Single().Current).IsEqualTo(included);
        await Assert.That(connectResults.Data.Lookup(included.Key).Value).IsEqualTo(included);
    }

    [Test]
    public async Task AnonymousQueryClonesCacheAndExposesReadOnlySnapshotMembers()
    {
        var cache = new Cache<CacheItem, string>();
        var original = new CacheItem("golf", 7);
        var replacement = new CacheItem("golf", 70);
        cache.AddOrUpdate(original, original.Key);

        var query = new AnonymousQuery<CacheItem, string>(cache);
        cache.AddOrUpdate(replacement, replacement.Key);

        await Assert.That(query.Count).IsEqualTo(1);
        await Assert.That(query.Items.Single()).IsEqualTo(original);
        await Assert.That(query.Keys.Single()).IsEqualTo(original.Key);
        await Assert.That(query.KeyValues.Single().Value).IsEqualTo(original);
        await Assert.That(query.Lookup(original.Key).Value).IsEqualTo(original);
    }

    [Test]
    public async Task InternalCacheRefreshNoOpsAndRemoveEnumerableOverloadsPreserveExpectedState()
    {
        var cache = new Cache<CacheItem, string>();
        var first = new CacheItem("hotel", 8);
        var second = new CacheItem("india", 9);
        var third = new CacheItem("juliet", 10);
        cache.AddOrUpdate(first, first.Key);
        cache.AddOrUpdate(second, second.Key);
        cache.AddOrUpdate(third, third.Key);

        cache.Refresh();
        cache.Refresh(first.Key);
        cache.Refresh(new[] { first.Key, second.Key });
        cache.Remove(new[] { first.Key });
        cache.Remove(Yield(second.Key));

        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(cache.Lookup(third.Key).Value).IsEqualTo(third);
        await Assert.That(cache.Lookup(first.Key).HasValue).IsFalse();
        await Assert.That(cache.Lookup(second.Key).HasValue).IsFalse();
    }

    [Test]
    public async Task ChangeAwareCacheRefreshEnumerableOverloadsCaptureOnlyExistingKeys()
    {
        var cache = new ChangeAwareCache<CacheItem, string>();
        var first = new CacheItem("kilo", 11);
        var second = new CacheItem("lima", 12);
        cache.AddOrUpdate(first, first.Key);
        cache.AddOrUpdate(second, second.Key);
        cache.CaptureChanges();

        cache.Refresh(new[] { first.Key, "missing-list" });
        cache.Refresh(Yield(second.Key, "missing-enumerable"));

        var changes = cache.CaptureChanges();

        await Assert.That(changes.Count).IsEqualTo(2);
        await Assert.That(changes.Select(static change => change.Reason)).IsEquivalentTo(new[] { ChangeReason.Refresh, ChangeReason.Refresh }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(changes.Select(static change => change.Current)).IsEquivalentTo(new[] { first, second }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task CacheUpdaterComparerAndKeyValueEnumerableBranchesUpdateRefreshAndRemove()
    {
        var cache = new ChangeAwareCache<CacheItem, string>();
        var updater = new CacheUpdater<CacheItem, string>(cache, static item => item.Key);
        var original = new CacheItem("mike", 13);
        var equivalent = new CacheItem("mike", 13);
        var changed = new CacheItem("mike", 14);
        var added = new CacheItem("november", 15);
        var comparer = CacheItemValueComparer.Instance;

        updater.AddOrUpdate(original);
        cache.CaptureChanges();

        updater.AddOrUpdate(equivalent, comparer);
        updater.AddOrUpdate(changed, comparer);
        updater.AddOrUpdate(Yield(added), comparer);
        updater.Refresh(Yield(changed));
        updater.Refresh(Yield(added.Key));
        updater.Remove(Yield(new KeyValuePair<string, CacheItem>(changed.Key, changed), new KeyValuePair<string, CacheItem>(added.Key, added)));

        var changes = cache.CaptureChanges();

        await Assert.That(cache.Count).IsEqualTo(0);
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Update)).IsEqualTo(1);
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Add)).IsEqualTo(1);
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Refresh)).IsEqualTo(2);
        await Assert.That(changes.Count(static change => change.Reason == ChangeReason.Remove)).IsEqualTo(2);
    }

    [Test]
    public async Task CacheUpdaterComparerAndProjectionOverloadsThrowWhenKeySelectorIsMissing()
    {
        var updater = new CacheUpdater<CacheItem, string>(new ChangeAwareCache<CacheItem, string>());
        var item = new CacheItem("oscar", 16);
        var comparer = EqualityComparer<CacheItem>.Default;

        await Assert.That(() => updater.AddOrUpdate(new[] { item }, comparer)).Throws<KeySelectorException>();
        await Assert.That(() => updater.AddOrUpdate(item, comparer)).Throws<KeySelectorException>();
        await Assert.That(() => updater.GetKeyValues(new[] { item }).ToArray()).Throws<KeySelectorException>();
    }

    private static IEnumerable<T> Yield<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    private sealed record CacheItem(string Key, int Value);

    private sealed class CacheItemValueComparer : IEqualityComparer<CacheItem>
    {
        public static readonly CacheItemValueComparer Instance = new();

        public bool Equals(CacheItem? x, CacheItem? y) => x?.Value == y?.Value;

        public int GetHashCode(CacheItem obj) => obj.Value.GetHashCode();
    }
}
