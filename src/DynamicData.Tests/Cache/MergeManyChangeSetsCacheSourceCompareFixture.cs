using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public sealed class MergeManyChangeSetsCacheSourceCompareFixture : IDisposable
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

    public MergeManyChangeSetsCacheSourceCompareFixture()
    {
        _randomizer = new(0x10012022);
        _marketFaker = Fakers.Market.RuleFor(m => m.Rating, faker => faker.Random.Double(0, 5)).WithSeed(_randomizer);
        _marketCacheResults = _marketCache.Connect().AsAggregator();
    }

    [Test]
    [Arguments(5, 7)]
    [Arguments(10, 50)]
#if false && !DEBUG
    [Arguments(100, 100)]
    [Arguments(10, 1_000)]
    [Arguments(1_000, 10)]
#endif
    public async Task MultiThreadedStressTest(int marketCount, int priceCount)
    {
        const int MaxItemId = 50;
        var MaxAddTime = TimeSpan.FromSeconds(0.250);
        var MaxRemoveTime = TimeSpan.FromSeconds(0.100);

        TimeSpan? GetRemoveTime() => _randomizer.Bool() ? _randomizer.TimeSpan(MaxRemoveTime) : null;

        IObservable<Unit> AddRemoveStress(int marketCount, int priceCount, int parallel, IScheduler scheduler) =>
            Observable.Create<Unit>(observer => new CompositeDisposable
                {
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
                });

        IObservable<IMarket> AddRemoveMarkets(int ownerCount, int parallel, IScheduler scheduler) =>
            _marketFaker.IntervalGenerate(MaxAddTime, scheduler)
                .Parallelize(ownerCount, parallel, obs => obs.StressAddRemove(_marketCache, _ => GetRemoveTime(), scheduler))
                .Finally(_marketCache.Dispose);

        IObservable<MarketPrice> AddRemovePrices(Market market, int priceCount, int parallel, IScheduler scheduler) =>
            _randomizer.Interval(MaxAddTime, scheduler).Select(_ => market.CreatePrice(_randomizer.Number(MaxItemId), GetRandomPrice()))
                .Parallelize(priceCount, parallel, obs => obs.StressAddRemove(market.PricesCache, _ => GetRemoveTime(), scheduler))
                .Finally(market.PricesCache.Dispose);

        var merged = _marketCache.Connect().MergeManyChangeSets(market => market.LatestPrices, Market.RatingCompare, resortOnSourceRefresh: true).Publish();
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
        await CheckResultContents(_marketCacheResults, priceResults, Market.RatingCompare);
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
        var actionParentCompare1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, sourceComparer: emptyParentComparer);
        var actionParentCompareKey1a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: emptyParentComparer);
        var actionParentCompareKey1b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector, sourceComparer: emptyParentComparer);
        var actionParentCompareKey1c = () => emptyChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: nullParentComparer);
        var actionParentCompare2 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, sourceComparer: emptyParentComparer, equalityComparer: emptyEqualityComparer);
        var actionParentCompareKey2a = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector, sourceComparer: emptyParentComparer, equalityComparer: emptyEqualityComparer);
        var actionParentCompareKey2b = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector, sourceComparer: emptyParentComparer, equalityComparer: emptyEqualityComparer);

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

        await Assert.That(actionParentCompare1).Throws<ArgumentNullException>();
        await Assert.That(actionParentCompareKey1a).Throws<ArgumentNullException>();
        await Assert.That(actionParentCompareKey1b).Throws<ArgumentNullException>();
        await Assert.That(actionParentCompareKey1c).Throws<ArgumentNullException>();
        await Assert.That(actionParentCompare2).Throws<ArgumentNullException>();
        await Assert.That(actionParentCompareKey2a).Throws<ArgumentNullException>();
        await Assert.That(actionParentCompareKey2b).Throws<ArgumentNullException>();
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
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory, Market.RatingCompare).Subscribe();

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
        using var sub = _marketCache.Connect().MergeManyChangeSets(factory, Market.RatingCompare).Subscribe();

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
        using var results = ChangeSetByRating().AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

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
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AllNewSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount);
        await Assert.That(markets.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AllRefreshedSubItemsAreRefreshed()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));
        _marketCache.AddOrUpdate(markets);

        // when
        markets.ForEach(m => m.RefreshAllPrices(GetRandomPrice));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount + 1);
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
        using var results = ChangeSetByRating().AsAggregator();
        markets[0].Rating = 1.0;
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
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeNoOpWhenRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        markets[0].Rating = 1.0;
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
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        markets[0].Rating = 1.0;
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
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldNotRefreshWhenHidden()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        markets[0].Rating = 1.0;
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
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task SourceRefreshGeneratesUpdatesAsNeeded()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        markets[0].Rating = 1.0;
        _marketCache.AddOrUpdate(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        SetRating(markets[1], 2.0);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(markets[1].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task SourceRefreshDoesNothingIfDisabled()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating(resortOnRefresh: false).AsAggregator();
        markets[0].Rating = 1.0;
        _marketCache.AddOrUpdate(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        SetRating(markets[1], 2.0);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        foreach (var pair in results.Data.Items.Zip(markets[0].PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AnyRemovedSubItemIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount);
        await Assert.That(results.Data.Count).IsEqualTo(MarketCount * (PricesPerMarket - RemoveCount));
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount * 2);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(MarketCount * RemoveCount);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task AnySourceItemRemovedRemovesAllSourceValues()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        _marketCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount)));

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(MarketCount - RemoveCount);
        await Assert.That(results.Data.Count).IsEqualTo((MarketCount - RemoveCount) * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(PricesPerMarket * RemoveCount);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task ChangingSourceByUpdateRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = ChangeSetByRating(false).AsAggregator();
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
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var pair in results.Data.Items.Zip(updatedMarket.PricesCache.Items)) { await Assert.That(pair.First).IsEqualTo(pair.Second); }
    }

    [Test]
    public async Task ChangingSourceByUpdateRemovesPreviousAndEmitsBetterValues()
    {
        // having
        using var results = ChangeSetByRating(false).AsAggregator();
        var market = new Market(0);
        var marketWorse = new Market(1);
        SetRating(marketWorse, -1);
        market.SetPrices(0, PricesPerMarket * 2, GetRandomPrice);
        marketWorse.SetPrices(0, PricesPerMarket * 2, GetRandomPrice);
        _marketCache.AddOrUpdate(market);
        _marketCache.AddOrUpdate(marketWorse);

        var updatedMarket = new Market(market);
        updatedMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, GetRandomPrice);

        // when
        _marketCache.AddOrUpdate(updatedMarket);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Take(PricesPerMarket).Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketWorse.Id); }
        foreach (var guid in results.Data.Items.Skip(PricesPerMarket).Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(updatedMarket.Id); }
    }

    [Test]
    public async Task UpdatesToCorrectValueOnRemove()
    {
        // having
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        var marketBest = new Market(2);
        marketBetter.Rating = 1.0;
        marketBest.Rating = 5.0;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketBest.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBest);
        _marketCache.AddOrUpdate(marketBetter);
        using var results = ChangeSetByRating(false).AsAggregator();

        // when
        _marketCache.Remove(marketBest);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketBetter.Id); }
    }

    [Test]
    public async Task OnlyUpdatesOnDuplicateIfNewItemIsFromBetterParent()
    {
        // having
        using var results = ChangeSetByRating(false).AsAggregator();
        using var resultsLow = ChangeSetByLowRating(false).AsAggregator();
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = 1.0;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);

        // when
        _marketCache.AddOrUpdate(marketBetter);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketBetter.Id); }
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsLow.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task BestChoiceFromDuplicatesSelectedWhenChangeSetCreated()
    {
        // having
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = 1.0;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBetter);

        // when
        using var results = ChangeSetByRating(false).AsAggregator();
        using var resultsLow = ChangeSetByLowRating(false).AsAggregator();

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketBetter.Id); }
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsLow.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task OnlyAddsBetterValuesOnSourceUpdate()
    {
        // having
        using var results = ChangeSetByRating(false).AsAggregator();
        using var resultsLow = ChangeSetByLowRating(false).AsAggregator();
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = 1.0;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBetter);

        // when
        marketBetter.SetPrices(0, PricesPerMarket, GetRandomPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketBetter.Id); }
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsLow.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task UpdatesToCorrectValueOnRefresh()
    {
        // having
        using var results = ChangeSetByRating(false).AsAggregator();
        using var resultsLow = ChangeSetByLowRating(false).AsAggregator();
        using var resultsRefresh = ChangeSetByRating(true).AsAggregator();
        using var resultsLowRefresh = ChangeSetByLowRating(true).AsAggregator();
        var marketOriginal = new Market(0);
        var marketBetter = new Market(1);
        marketBetter.Rating = -1.0;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketBetter.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketBetter);

        // when
        SetRating(marketBetter, 2.0);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(_marketCacheResults.Summary.Overall.Refreshes).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsLow.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketBetter.Id); }
        await Assert.That(resultsRefresh.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsRefresh.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsRefresh.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsRefresh.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsRefresh.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketBetter.Id); }
        await Assert.That(resultsLowRefresh.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLowRefresh.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLowRefresh.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(resultsLowRefresh.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLowRefresh.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsLowRefresh.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
    }

    [Test]
    public async Task ChildComparerUpdatesToCorrectValueOnUpdate()
    {
        // having
        using var resultsLow = ChangeSetByLowRating(false).AsAggregator();
        using var resultsLowPrice = ChangeSetByRatingThenLowPrice(false).AsAggregator();
        using var resultsHighPrice = ChangeSetByRatingThenHighPrice(false).AsAggregator();
        var marketOriginal = new Market(0);
        var marketHighest = new Market(1);
        var marketLowest = new Market(2);
        marketLowest.Rating = marketHighest.Rating = 1.0;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketHighest.SetPrices(0, PricesPerMarket, HighestPrice);
        marketLowest.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketHighest);
        _marketCache.AddOrUpdate(marketLowest);

        // when
        marketLowest.UpdateAllPrices(LowestPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(3);
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsLow.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }

        await Assert.That(resultsLowPrice.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLowPrice.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLowPrice.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(resultsLowPrice.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLowPrice.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsLowPrice.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLowest.Id); }

        await Assert.That(resultsHighPrice.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsHighPrice.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsHighPrice.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsHighPrice.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsHighPrice.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in resultsHighPrice.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketHighest.Id); }
    }

    [Test]
    public async Task ChildComparerOnlyUpdatesVisibleValuesOnUpdate()
    {
        // having
        using var results = ChangeSetByRating(false).AsAggregator();
        using var lowRatingLowPriceResults = ChangeSetByLowRatingThenLowPrice(false).AsAggregator();
        using var lowRatingHighPriceResults = ChangeSetByLowRatingThenHighPrice(false).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketLowest = new Market(2);

        marketLowest.Rating = marketLow.Rating = -1;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketLowest.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketLowest);

        // when
        marketLowest.UpdateAllPrices(LowestPrice - 1);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(3);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
        await Assert.That(lowRatingLowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowRatingLowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLowest.Id); }
        await Assert.That(lowRatingHighPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowRatingHighPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
    }

    [Test]
    public async Task ChildComparerOnlyRefreshesVisibleValues()
    {
        // having
        using var results = ChangeSetByRating(false).AsAggregator();
        using var lowRatingLowPriceResults = ChangeSetByLowRatingThenLowPrice(false).AsAggregator();
        using var lowRatingHighPriceResults = ChangeSetByLowRatingThenHighPrice(false).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketLowest = new Market(2);

        marketLowest.Rating = marketLow.Rating = -1;
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLowest.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(marketOriginal);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(marketLowest);

        // when
        marketLowest.RefreshAllPrices(LowestPrice - 1);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(3);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in results.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketOriginal.Id); }
        await Assert.That(lowRatingLowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(lowRatingLowPriceResults.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
        foreach (var guid in lowRatingLowPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLowest.Id); }
        await Assert.That(lowRatingHighPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowRatingHighPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        foreach (var guid in lowRatingHighPriceResults.Data.Items.Select(cp => cp.MarketId)) { await Assert.That(guid).IsEqualTo(marketLow.Id); }
    }

    [Test]
    public async Task EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = new Market(0);
        using var results = CreateChangeSet("Equality Compare", Market.RatingCompare, equalityComparer: MarketPrice.EqualityComparer, resortOnRefresh: true).AsAggregator();
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
    public async Task EqualityComparerAndChildComparerWorkTogetherForUpdates()
    {
        // having
        using var resultsLow = ChangeSetByLowRating().AsAggregator();
        using var resultsRecent = ChangeSetByRatingThenRecent().AsAggregator();
        using var resultsTimeStamp = ChangeSetByRatingThenTimeStamp().AsAggregator();
        var marketLow = new Market(0);
        var market = new Market(1);
        marketLow.Rating = -1;
        marketLow.SetPrices(0, PricesPerMarket, GetRandomPrice);
        market.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(market);
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        market.UpdateAllPrices(LowestPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Messages.Count).IsEqualTo(1);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(resultsRecent.Messages.Count).IsEqualTo(3);
        await Assert.That(resultsRecent.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsRecent.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsRecent.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(resultsRecent.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(resultsTimeStamp.Messages.Count).IsEqualTo(4);
        await Assert.That(resultsTimeStamp.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsTimeStamp.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsTimeStamp.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(resultsTimeStamp.Summary.Overall.Refreshes).IsEqualTo(0);
    }

    [Test]
    public async Task EqualityComparerAndChildComparerWorkTogetherForRefreshes()
    {
        // having
        using var resultsLow = ChangeSetByLowRating().AsAggregator();
        using var resultsRecent = ChangeSetByRatingThenRecent().AsAggregator();
        using var resultsTimeStamp = ChangeSetByRatingThenTimeStamp().AsAggregator();
        var marketLow = new Market(0);
        var market = new Market(1);
        marketLow.Rating = -1;
        marketLow.SetPrices(0, PricesPerMarket, GetRandomPrice);
        market.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(market);
        market.SetPrices(0, PricesPerMarket, LowestPrice);
        // Update again, but only the timestamp will change, so resultsRecent will ignore
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        // resultsRecent won't see the refresh because it ignored the update
        // resultsTimeStamp will see the refreshes because it didn't
        market.RefreshAllPrices(LowestPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Messages.Count).IsEqualTo(1);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(resultsRecent.Messages.Count).IsEqualTo(4);
        await Assert.That(resultsRecent.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsRecent.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsRecent.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(resultsRecent.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsTimeStamp.Messages.Count).IsEqualTo(5);
        await Assert.That(resultsTimeStamp.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsTimeStamp.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsTimeStamp.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(resultsTimeStamp.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
    }

    [Test]
    public async Task EqualityComparerAndChildComparerRefreshesBecomeUpdates()
    {
        // having
        using var resultsLow = ChangeSetByLowRating().AsAggregator();
        using var resultsRecent = ChangeSetByRatingThenRecent().AsAggregator();
        using var resultsTimeStamp = ChangeSetByRatingThenTimeStamp().AsAggregator();
        var marketLow = new Market(0);
        var market = new Market(1);
        marketLow.Rating = -1;
        marketLow.SetPrices(0, PricesPerMarket, GetRandomPrice);
        market.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketCache.AddOrUpdate(marketLow);
        _marketCache.AddOrUpdate(market);
        market.SetPrices(0, PricesPerMarket, LowestPrice);
        // Update again, but only the timestamp will change, so resultsRecent will ignore
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // when
        // resultsRecent won't see the refresh because it ignored the update
        // resultsTimeStamp will see the refreshes because it didn't
        market.RefreshAllPrices(GetRandomPrice);

        // then
        await Assert.That(_marketCacheResults.Data.Count).IsEqualTo(2);
        await Assert.That(resultsLow.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Messages.Count).IsEqualTo(1);
        await Assert.That(resultsLow.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsLow.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(resultsLow.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(resultsRecent.Messages.Count).IsEqualTo(4);
        await Assert.That(resultsRecent.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsRecent.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsRecent.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(resultsRecent.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(resultsTimeStamp.Messages.Count).IsEqualTo(5);
        await Assert.That(resultsTimeStamp.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(resultsTimeStamp.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(resultsTimeStamp.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(resultsTimeStamp.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
    }

    [Test]
    public async Task EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        _marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

        // when
        using var results = ChangeSetByRating(false).AsAggregator();
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
                            .MergeManyChangeSets(m => m.LatestPrices, Market.RatingCompare).Subscribe(_ => { }, () => hasMergedSequenceCompleted = true);

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
                            .MergeManyChangeSets(m => m.LatestPrices, Market.RatingCompare).Subscribe(_ => { }, err => receivedError = err);

        // when
        DisposeMarkets();

        // then
        await Assert.That(receivedError).IsEqualTo(expectedError);
    }

    private IObservable<IChangeSet<MarketPrice, int>> CreateChangeSet(string name, IComparer<IMarket>? sourceComp = null, IComparer<MarketPrice>? childCompare = null, IEqualityComparer<MarketPrice>? equalityComparer = null, bool resortOnRefresh = true) =>
        _marketCache.Connect()
            .DebugSpy(name)
            .MergeManyChangeSets(m => m.LatestPrices.DebugSpy($"{name} [{m.Name} Prices]"), sourceComp ?? Market.RatingCompare, resortOnSourceRefresh: resortOnRefresh, equalityComparer, childCompare)
            .DebugSpy($"{name} [Results]");

    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRating(bool resortOnRefresh = true) => CreateChangeSet("Rating", resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenHighPrice(bool resortOnRefresh = true) => CreateChangeSet("Rating | High", Market.RatingCompare, MarketPrice.HighPriceCompare, resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenLowPrice(bool resortOnRefresh = true) => CreateChangeSet("Rating | Low", Market.RatingCompare, MarketPrice.LowPriceCompare, resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenRecent(bool resortOnRefresh = true) => CreateChangeSet("Rating | Recent", Market.RatingCompare, MarketPrice.LatestPriceCompare, equalityComparer: MarketPrice.EqualityComparer, resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByRatingThenTimeStamp(bool resortOnRefresh = true) => CreateChangeSet("Rating | Timestamp", Market.RatingCompare, MarketPrice.LatestPriceCompare, equalityComparer: MarketPrice.EqualityComparerWithTimeStamp, resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByLowRating(bool resortOnRefresh = true) => CreateChangeSet("Low Rating", Market.RatingCompare.Invert(), resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByLowRatingThenHighPrice(bool resortOnRefresh = true) => CreateChangeSet("Low Rating | High", Market.RatingCompare.Invert(), MarketPrice.HighPriceCompare, resortOnRefresh: resortOnRefresh);
    private IObservable<IChangeSet<MarketPrice, int>> ChangeSetByLowRatingThenLowPrice(bool resortOnRefresh = true) => CreateChangeSet("Low Rating | Low", Market.RatingCompare.Invert(), MarketPrice.LowPriceCompare, resortOnRefresh: resortOnRefresh);

    private IMarket SetRating(IMarket market, double newRating)
    {
        market.Rating = newRating;
        _marketCache.Refresh(market);
        return market;
    }

    public void Dispose()
    {
        _marketCacheResults.Dispose();
        _marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
        _marketCache.Dispose();
    }

    private async Task CheckResultContents(ChangeSetAggregator<IMarket, Guid> marketResults, ChangeSetAggregator<MarketPrice, int> priceResults, IComparer<IMarket> comparer)
    {
        var expectedMarkets = _marketCache.Items.ToList();

        // These should be subsets of each other
        await Assert.That(expectedMarkets.Except(marketResults.Data.Items).Any()).IsFalse();
        await Assert.That(marketResults.Data.Items.Count).IsEqualTo(expectedMarkets.Count);

        // Pair up all the Markets/Prices, Group them by ItemId, and sort each Group by the Market comparer
        // Then pull out the first value from each group, which should be the price from the best market for each ItemId
        var expectedPrices = expectedMarkets.Select(m => (Market)m).SelectMany(m => m.PricesCache.Items.Select(mp => (Market: m, MarketPrice: mp)))
            .GroupBy(tuple => tuple.MarketPrice.ItemId)
            .Select(group => group.OrderBy(tuple => tuple.Market, comparer).Select(tuple => tuple.MarketPrice).First())
            .ToList();

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
