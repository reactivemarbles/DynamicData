using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public sealed class MergeManyChangeSetsCacheFixture : IDisposable
{
#if DEBUG
    const int MarketCount = 5;
    const int PricesPerMarket = 7;
    const int RemoveCount = 3;
#else
    const int MarketCount = 101;
    const int PricesPerMarket = 103;
    const int RemoveCount = 53;
#endif

    const int ItemIdStride = 1000;
    const decimal BasePrice = 10m;
    const decimal PriceOffset = 10m;
    const decimal HighestPrice = BasePrice + PriceOffset + 1.0m;
    const decimal LowestPrice = BasePrice - 1.0m;

    private readonly ISourceCache<IMarket, Guid> _marketCache = new SourceCache<IMarket, Guid>(p => p.Id);

    private readonly ChangeSetAggregator<IMarket, Guid> _marketCacheResults;

    private readonly Faker<Market> _marketFaker;

    private readonly Randomizer _randomizer;

    public MergeManyChangeSetsCacheFixture()
    {
        _randomizer = new(0x21123737);
        _marketFaker = Fakers.Market.Clone().WithSeed(_randomizer);
        _marketCacheResults = _marketCache.Connect().AsAggregator();
    }

    [Test]
    [Arguments(5, 7)]
    [Arguments(10, 50)]
#if !DEBUG
    [Arguments(10, 1_000)]
    [Arguments(200, 500)]
    [Arguments(1_000, 10)]
#endif
    public async Task MultiThreadedStressTest(int marketCount, int priceCount)
    {
        var MaxAddTime = TimeSpan.FromSeconds(0.250);
        var MaxRemoveTime = TimeSpan.FromSeconds(0.100);

        TimeSpan? GetRemoveTime() => _randomizer.Bool() ? _randomizer.TimeSpan(MaxRemoveTime) : null;

        IObservable<Unit> AddRemoveStress(int marketCount, int priceCount, int parallel, IScheduler scheduler) =>
            Observable.Create<Unit>(observer => new CompositeDisposable
                (
                    AddRemoveMarkets(marketCount, parallel, scheduler)
                        .Subscribe(
                            onNext: static _ => { },
                            onError: observer.OnError),

                    _marketCache.Connect()
                        .MergeMany(market => AddRemovePrices((Market)market, priceCount, parallel, scheduler))
                        .Subscribe(
                            onNext: static _ => { },
                            onError: observer.OnError,
                            onCompleted: observer.OnCompleted)
                ));

        IObservable<IMarket> AddRemoveMarkets(int ownerCount, int parallel, IScheduler scheduler) =>
            _marketFaker.IntervalGenerate(MaxAddTime, scheduler)
                .Parallelize(ownerCount, parallel, obs => obs.StressAddRemove(_marketCache, _ => GetRemoveTime(), scheduler))
                .Finally(_marketCache.Dispose);

        IObservable<MarketPrice> AddRemovePrices(Market market, int priceCount, int parallel, IScheduler scheduler) =>
            _randomizer.Interval(MaxAddTime, scheduler).Select(_ => market.CreateUniquePrice(_ => GetRandomPrice()))
                .Parallelize(priceCount, parallel, obs => obs.StressAddRemove(market.PricesCache, _ => GetRemoveTime(), scheduler))
                .Finally(market.PricesCache.Dispose);

        var merged = _marketCache.Connect().MergeManyChangeSets(market => market.LatestPrices).Publish();
        var adding = true;
        var cacheCompleted = merged.LastOrDefaultAsync().ToTask();
        using var priceResults = merged.AsAggregator();
        using var connect = merged.Connect();

        // Start asynchrononously modifying the parent list and the child lists
        using var addingSub = AddRemoveStress(marketCount, priceCount, Environment.ProcessorCount, TaskPoolScheduler.Default)
            .Finally(() => adding = false)
            .Subscribe();

        // Subscribe / unsubscribe over and over while the collections are being modified
        do
        {
            // Ensure items are being added asynchronously before subscribing to changes
            await Task.Yield();

            {
                // Subscribe
                var mergedSub = merged.Subscribe();

                // Let other threads run
                await Task.Yield();

                // Unsubscribe
                mergedSub.Dispose();
            }
        }
        while (adding);

        // Wait for the source cache to finish delivering all notifications.
        await cacheCompleted;

        // Verify the results
        await CheckResultContents(_marketCacheResults, priceResults);
    }

    [Test]
    public async Task NullChecks()
    {
        // having
        var emptyChangeSetObs = Observable.Empty<IChangeSet<int, int>>();
        var nullChangeSetObs = (IObservable<IChangeSet<int, int>>)null!;
        var emptyChildChangeSetObs = Observable.Empty<IChangeSet<string, string>>();
        var emptySelector = new Func<int, IObservable<IChangeSet<string, string>>>(i => emptyChildChangeSetObs);
        var emptyKeySelector = new Func<int, int, IObservable<IChangeSet<string, string>>>((i, key) => emptyChildChangeSetObs);
        var nullSelector = (Func<int, IObservable<IChangeSet<string, string>>>)null!;
        var nullKeySelector = (Func<int, int, IObservable<IChangeSet<string, string>>>)null!;
        var nullParentComparer = (IComparer<int>)null!;
        var emptyParentComparer = new NoOpComparer<int>() as IComparer<int>;
        var nullChildComparer = (IComparer<string>)null!;
        var emptyChildComparer = new NoOpComparer<string>() as IComparer<string>;
        var nullEqualityComparer = (IEqualityComparer<string>)null!;
        var emptyEqualityComparer = new NoOpEqualityComparer<string>() as IEqualityComparer<string>;

        // when
        var actionDefault1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector);
        var actionDefault2a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector);
        var actionDefault2b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector);
        var actionChildCompare1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, comparer: emptyChildComparer);
        var actionChildCompare2a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector, comparer: emptyChildComparer);
        var actionChildCompare2b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector, comparer: emptyChildComparer);
        var actionChildCompare2c = () => emptyChangeSetObs.MergeManyChangeSets(emptyKeySelector, comparer: nullChildComparer);

        // then
        await Assert.That(emptyChangeSetObs).IsNotNull();
        await Assert.That(emptyChildChangeSetObs).IsNotNull();
        await Assert.That(emptyChildComparer).IsNotNull();
        await Assert.That(emptyEqualityComparer).IsNotNull();
        await Assert.That(emptyKeySelector).IsNotNull();
        await Assert.That(emptyParentComparer).IsNotNull();
        await Assert.That(emptySelector).IsNotNull();
        await Assert.That(nullChangeSetObs).IsNull();
        await Assert.That(nullChildComparer).IsNull();
        await Assert.That(nullEqualityComparer).IsNull();
        await Assert.That(nullKeySelector).IsNull();
        await Assert.That(nullParentComparer).IsNull();
        await Assert.That(nullSelector).IsNull();

        await Assert.That(actionDefault1).Throws<ArgumentNullException>();
        await Assert.That(actionDefault2a).Throws<ArgumentNullException>();
        await Assert.That(actionDefault2b).Throws<ArgumentNullException>();
        await Assert.That(actionChildCompare1).Throws<ArgumentNullException>();
        await Assert.That(actionChildCompare2a).Throws<ArgumentNullException>();
        await Assert.That(actionChildCompare2b).Throws<ArgumentNullException>();
        await Assert.That(actionChildCompare2c).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AbleToInvokeFactory()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<MarketPrice, int>> factory(IMarket m)
        {
            invoked = true;
            return m.LatestPrices;
        }
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(1);
        await Assert.That(invoked).IsTrue();
    }

    [Test]
    public async Task AbleToInvokeFactoryWithKey()
    {
        // having
        var invoked = false;
        IObservable<IChangeSet<MarketPrice, int>> factory(IMarket m, Guid g)
        {
            invoked = true;
            return m.LatestPrices;
        }
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketCache.AddOrUpdate(new Market(0));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(1);
        await Assert.That(invoked).IsTrue();
    }

    [Test]
    public async Task AllExistingSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        AddUniquePrices(markets);

        // when
        _marketCache.AddOrUpdate(markets);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount);
        await Assert.That(markets.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AllNewSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        AddUniquePrices(markets);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount);
        await Assert.That(markets.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AllRefreshedSubItemsAreRefreshed()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        AddUniquePrices(markets);

        // when
        markets.ForEach(m => m.RefreshAllPrices(GetRandomPrice));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount * 2);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(MarketCount * PricesPerMarket);
    }

    [Test]
    public async Task AnyDuplicateKeyValuesShouldBeHidden()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(markets[0].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeNoOpWhenRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        markets[1].RemoveAllPrices();

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(markets[0].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        _marketCache.Remove(markets[0]);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(markets[1].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(PricesPerMarket);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldNotRefreshWhenHidden()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        markets[1].RefreshAllPrices(GetRandomPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var pair in results.Data.Items.Zip(markets[0].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
    }

    [Test]
    public async Task AnyRemovedSubItemIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketCache.AddOrUpdate(markets);
        AddUniquePrices(markets);

        // when
        markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * (PricesPerMarket - RemoveCount));
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount * 2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(MarketCount * RemoveCount);
    }

    [Test]
    public async Task AnySourceItemRemovedRemovesAllSourceValues()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        AddUniquePrices(markets);
        _marketCache.AddOrUpdate(markets);

        // when
        _marketCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount)));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount - RemoveCount);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo((MarketCount - RemoveCount) * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(PricesPerMarket * RemoveCount);
    }

    [Test]
    public async Task ClearingParentEmitsSingleChangeSet()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        AddUniquePrices(markets);
        _marketCache.AddOrUpdate(markets);

        // when
        _marketCache.Clear();

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(0);
        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task ChangingSourceByUpdateRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.SetPrices(0, PricesPerMarket * 2, GetRandomPrice);
        _marketCache.AddOrUpdate(market);
        var updatedMarket = new Market(market);
        updatedMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, GetRandomPrice);

        // when
        _marketCache.AddOrUpdate(updatedMarket);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(updatedMarket.PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
    }

    [Test]
    public async Task ComparerOnlyAddsBetterAddedValues()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);

        // when
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(3);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketHigh.Id); }
    }

    [Test]
    public async Task ComparerOnlyAddsBetterExistingValues()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(3);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketHigh.Id); }
    }

    [Test]
    public async Task ComparerOnlyAddsBetterValuesOnSourceUpdate()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketLowLow = new Market(marketLow);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketLowLow.SetPrices(0, PricesPerMarket, LowestPrice - 1);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);

        // when
        _marketCache.AddOrUpdate(marketLowLow);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLowLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnRefresh()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketFlipFlop);

        // when
        marketFlipFlop.RefreshAllPrices(LowestPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketFlipFlop.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnRemove()
    {
        // having
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketHigh);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketCache.Remove(marketLow);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketHigh.Id); }
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnUpdate()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketFlipFlop);

        // when
        marketFlipFlop.UpdateAllPrices(LowestPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketFlipFlop.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ComparerOnlyUpdatesVisibleValuesOnUpdate()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);

        // when
        marketLow.UpdateAllPrices(LowestPrice - 1);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ComparerOnlyRefreshesVisibleValues()
    {
        // having
        using var highPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);

        // when
        marketLow.RefreshAllPrices(LowestPrice - 1);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
        foreach (var guid in lowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in highPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = new Market(0);
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        market.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(market);

        // when
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        _marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

        // when
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices).AsAggregator();
        DisposeMarkets();

        // then
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket * MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket * MarketCount);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task MergedObservableCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
    {
        // having
        _marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)));
        var hasSourceSequenceCompleted = false;
        var hasMergedSequenceCompleted = false;

        using var cleanup = _marketCache.Connect().Do(_ => { }, () => hasSourceSequenceCompleted = true)
                            .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, () => hasMergedSequenceCompleted = true);

        // when
        if (completeSource)
        {
            DisposeMarkets();
        }

        // then
        await Assert.That(hasSourceSequenceCompleted).IsEqualTo(completeSource);
        await Assert.That(hasMergedSequenceCompleted).IsEqualTo(completeSource && completeChildren);
    }

    [Test]
    public async Task MergedObservableWillFailIfSourceFails()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        _marketCache.AddOrUpdate(markets);
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<IMarket, Guid>>(expectedError);

        using var cleanup = _marketCache.Connect().Concat(throwObservable)
                            .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, err => receivedError = err);

        // when
        DisposeMarkets();

        // then
        await Assert.That(receivedError).IsEqualTo(expectedError);
    }

    [Test]
    public async Task MergeManyChangeSetsWorksCorrectlyWithValueTypes()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        _marketCache.AddOrUpdate(markets);
        markets.ForEach(m => m.SetPrices(0, PricesPerMarket, GetRandomPrice));
        using var results = _marketCache.Connect()
                .MergeManyChangeSets(m => m.LatestPrices.Transform(p => p.Price))
                .AsAggregator();

        // when
        markets.ForEach(m => m.RemoveAllPrices());

        // then
        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(PricesPerMarket);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OrderOfChangesIsPreserved(bool removeFirst)
    {
        // Arrange
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        AddUniquePrices(markets);
        _marketCache.AddOrUpdate(markets);
        var markets2 = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        AddUniquePrices(markets2);
        using var results = _marketCache.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        (var firstReason, var nextReason, int expectedChanges) = removeFirst
            ? (ChangeReason.Remove, ChangeReason.Add, 2 * MarketCount * PricesPerMarket)
            : (ChangeReason.Add, ChangeReason.Remove, 3 * MarketCount * PricesPerMarket);

        // Act
        _marketCache.Edit(updater =>
        {
            if (removeFirst)
            {
                updater.Clear();
                updater.AddOrUpdate(markets2);
            }
            else
            {

                updater.AddOrUpdate(markets2);
                updater.Clear();
            }
        });

        // Assert
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[0].All(change => change.Reason is ChangeReason.Add)).IsTrue();
        await Assert.That(results.Messages[1].Count).IsEqualTo(expectedChanges);
        await Assert.That(results.Messages[1].Take(MarketCount * PricesPerMarket).All(change => change.Reason == firstReason)).IsTrue();
        await Assert.That(results.Messages[1].Skip(MarketCount * PricesPerMarket).All(change => change.Reason == nextReason)).IsTrue();
    }

    public void Dispose()
    {
        _marketCacheResults.Dispose();
        _marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
        _marketCache.Dispose();
    }

    private void AddUniquePrices(Market[] markets) => markets.ForEach(m => m.AddUniquePrices(PricesPerMarket, _ => GetRandomPrice()));

    private async Task CheckResultContents(ChangeSetAggregator<IMarket, Guid> marketResults, ChangeSetAggregator<MarketPrice, int> priceResults)
    {
        var expectedMarkets = _marketCache.Items.ToList();
        var expectedPrices = expectedMarkets.SelectMany(market => ((Market)market).PricesCache.Items).ToList();

        // These should be subsets of each other
        await Assert.That(expectedMarkets.Except(marketResults.Data.Items).Any()).IsFalse();
        await Assert.That(marketResults.Data.Items.Count).IsEqualTo(expectedMarkets.Count);

        // These should be subsets of each other
        await Assert.That(expectedPrices.Except(priceResults.Data.Items).Any()).IsFalse();
        await Assert.That(priceResults.Data.Items.Count).IsEqualTo(expectedPrices.Count);
    }

    private void DisposeMarkets()
    {
        _marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
        _marketCache.Dispose();
        _marketCache.Clear();
    }

    private decimal GetRandomPrice() => MarketPrice.RandomPrice(_randomizer, BasePrice, PriceOffset);
}
