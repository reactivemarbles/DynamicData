using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public sealed class MergeManyChangeSetsCacheFixture : IDisposable
{
#if DEBUG
    const int MarketCount = 3;
    const int PricesPerMarket = 5;
    const int RemoveCount = 2;
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

    private readonly ISourceList<IMarket> _marketList = new SourceList<IMarket>();

    private readonly ChangeSetAggregator<IMarket> _marketListResults;

    private readonly Faker<Market> _marketFaker;

    private readonly Randomizer _randomizer;

    public MergeManyChangeSetsCacheFixture()
    {
        _randomizer = new(0x03251976);
        _marketFaker = Fakers.Market.WithSeed(_randomizer);
        _marketListResults = _marketList.Connect().AsAggregator();
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

                    _marketList.Connect()
                        .MergeMany(market => AddRemovePrices((Market)market, priceCount, parallel, scheduler))
                        .Subscribe(
                            onNext: static _ => { },
                            onError: observer.OnError,
                            onCompleted: observer.OnCompleted)
                ));

        IObservable<IMarket> AddRemoveMarkets(int ownerCount, int parallel, IScheduler scheduler) =>
            _marketFaker.IntervalGenerate(MaxAddTime, scheduler)
                .Parallelize(ownerCount, parallel, obs => obs.StressAddRemove(_marketList, _ => GetRemoveTime(), scheduler))
                .Finally(_marketList.Dispose);

        IObservable<MarketPrice> AddRemovePrices(Market market, int priceCount, int parallel, IScheduler scheduler) =>
            _randomizer.Interval(MaxAddTime, scheduler).Select(_ => market.CreateUniquePrice(_ => GetRandomPrice()))
                .Parallelize(priceCount, parallel, obs => obs.StressAddRemove(market.PricesCache, _ => GetRemoveTime(), scheduler))
                .Finally(market.PricesCache.Dispose);

        var merged = _marketList.Connect().MergeManyChangeSets(market => market.LatestPrices);
        using var priceResults = merged.AsAggregator();

        var adding = true;

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

        // Verify the results
        await WaitForResultContentsAsync(_marketListResults, priceResults, TimeSpan.FromSeconds(5));
        await CheckResultContents(_marketListResults, priceResults);
    }

    [Test]
    public async Task NullChecks()
    {
        // having
        var emptyChangeSetObs = Observable.Empty<IChangeSet<int>>();
        var nullChangeSetObs = (IObservable<IChangeSet<int>>)null!;
        var emptySelector = new Func<int, IObservable<IChangeSet<string, string>>>(i => Observable.Empty<IChangeSet<string, string>>());
        var nullSelector = (Func<int, IObservable<IChangeSet<string, string>>>)null!;
        var nullListOfCacheChangeSets = (ISourceList<IObservable<IChangeSet<string, string>>>)null!;
        var emptyListOfCacheChangeSets = new SourceList<IObservable<IChangeSet<string, string>>>();
        var nullChildComparer = (IComparer<string>)null!;
        var emptyChildComparer = new NoOpComparer<string>() as IComparer<string>;
        var emptyEqualityComparer = new NoOpEqualityComparer<string>() as IEqualityComparer<string>;

        // when
        var actionDefault0 = () => nullChangeSetObs.MergeManyChangeSets(emptySelector, equalityComparer: emptyEqualityComparer, comparer: emptyChildComparer);
        var actionDefault1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, equalityComparer: emptyEqualityComparer, comparer: emptyChildComparer);
        var actionComparer0 = () => nullChangeSetObs.MergeManyChangeSets(emptySelector, comparer: emptyChildComparer);
        var actionComparer1 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector, comparer: emptyChildComparer);
        var actionComparer2 = () => emptyChangeSetObs.MergeManyChangeSets(emptySelector, comparer: nullChildComparer);
        var actionMergeChangeSets0 = () => nullListOfCacheChangeSets.MergeChangeSets(comparer: emptyChildComparer);

        // then
        await Assert.That(emptyChangeSetObs).IsNotNull();
        await Assert.That(emptyChildComparer).IsNotNull();
        await Assert.That(emptyEqualityComparer).IsNotNull();
        await Assert.That(emptySelector).IsNotNull();
        await Assert.That(emptyListOfCacheChangeSets).IsNotNull();
        await Assert.That(nullChangeSetObs).IsNull();
        await Assert.That(nullChildComparer).IsNull();
        await Assert.That(nullSelector).IsNull();
        await Assert.That(nullListOfCacheChangeSets).IsNull();

        await Assert.That(actionDefault0).Throws<ArgumentNullException>();
        await Assert.That(actionDefault1).Throws<ArgumentNullException>();
        await Assert.That(actionComparer0).Throws<ArgumentNullException>();
        await Assert.That(actionComparer1).Throws<ArgumentNullException>();
        await Assert.That(actionComparer2).Throws<ArgumentNullException>();
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
        using var sub = _marketList.Connect().MergeManyChangeSets(factory).Subscribe();

        // when
        _marketList.Add(new Market(0));

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(1);
        await Assert.That(invoked).IsTrue();
    }

    [Test]
    public async Task AllExistingSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        markets.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        _marketList.AddRange(markets);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(MarketCount);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);

        // when
        markets.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(MarketCount);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        markets.ForEach(m => m.RefreshAllPrices(GetRandomPrice));

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(MarketCount);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);

        // when
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Data.Items.Zip(markets[0].PricesCache.Items).All(pair => Equals(pair.First, pair.Second))).IsTrue();
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeNoOpWhenRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        markets[1].RemoveAllPrices();

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Data.Items.Zip(markets[0].PricesCache.Items).All(pair => Equals(pair.First, pair.Second))).IsTrue();
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        _marketList.Remove(markets[0]);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Data.Items.Zip(markets[1].PricesCache.Items).All(pair => Equals(pair.First, pair.Second))).IsTrue();
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Updates).IsEqualTo(PricesPerMarket);
    }

    [Test]
    public async Task AnyDuplicateValuesShouldNotRefreshWhenHidden()
    {
        // having
        var markets = Enumerable.Range(0, 2).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets[0].SetPrices(0, PricesPerMarket, GetRandomPrice);
        markets[1].SetPrices(0, PricesPerMarket, GetRandomPrice);

        // when
        markets[1].RefreshAllPrices(GetRandomPrice);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(results.Data.Items.Zip(markets[0].PricesCache.Items).All(pair => Equals(pair.First, pair.Second))).IsTrue();
    }

    [Test]
    public async Task AnyRemovedSubItemIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(MarketCount);
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
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        _marketList.RemoveRange(0, RemoveCount);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(MarketCount - RemoveCount);
        await Assert.That(results.Data.Count).IsEqualTo((MarketCount - RemoveCount) * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(PricesPerMarket * RemoveCount);
    }

    [Test]
    public async Task ChangingSourceByAddRemoveRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.SetPrices(0, PricesPerMarket * 2, GetRandomPrice);
        _marketList.Add(market);
        var otherMarket = new Market(1);
        otherMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, GetRandomPrice);

        // when
        _marketList.Add(otherMarket);
        _marketList.Remove(market);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Messages[1].Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Messages[2].Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Messages[2].Removes).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket * 3);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Data.Items.Except(otherMarket.PricesCache.Items).Any()).IsFalse();
        await Assert.That(otherMarket.PricesCache.Items.Except(results.Data.Items).Any()).IsFalse();
    }

    [Test]
    public async Task ChangingSourceByRemoveAddRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.SetPrices(0, PricesPerMarket * 2, GetRandomPrice);
        _marketList.Add(market);
        var otherMarket = new Market(1);
        otherMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, GetRandomPrice);

        // when
        _marketList.Remove(market);
        _marketList.Add(otherMarket);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages[0].Adds).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Messages[1].Removes).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Messages[2].Adds).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket * 4);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Data.Items.Except(otherMarket.PricesCache.Items).Any()).IsFalse();
        await Assert.That(otherMarket.PricesCache.Items.Except(results.Data.Items).Any()).IsFalse();
    }

    [Test]
    public async Task ChangingSourceByReplaceRemovesPreviousAndAddsNewValues()
    {
        // having
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        var market = new Market(0);
        market.SetPrices(0, PricesPerMarket * 2, GetRandomPrice);
        _marketList.Add(market);
        var otherMarket = new Market(1);
        otherMarket.SetPrices(PricesPerMarket, PricesPerMarket * 3, GetRandomPrice);

        // when
        _marketList.Replace(market, otherMarket);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(1);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(results.Data.Items.Except(otherMarket.PricesCache.Items).Any()).IsFalse();
        await Assert.That(otherMarket.PricesCache.Items.Except(results.Data.Items).Any()).IsFalse();
    }

    [Test]
    public async Task ComparerOnlyAddsBetterAddedValues()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);
        _marketList.Add(marketHigh);

        // when
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(3);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketLow.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketHigh.Id))).IsTrue();
    }

    [Test]
    public async Task ComparerOnlyAddsBetterExistingValues()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketList.Add(marketOriginal);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketList.Add(marketLow);
        _marketList.Add(marketHigh);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(3);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketLow.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketHigh.Id))).IsTrue();
    }

    [Test]
    public async Task ComparerOnlyAddsBetterValuesOnSourceReplace()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().DebugSpy("List").MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).DebugSpy("MergedLow").AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketLowLow = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketLowLow.SetPrices(0, PricesPerMarket, LowestPrice - 1);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);

        // when
        _marketList.Replace(marketLow, marketLowLow);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketLowLow.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketOriginal.Id))).IsTrue();
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnRefresh()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketFlipFlop);

        // when
        marketFlipFlop.RefreshAllPrices(LowestPrice);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketFlipFlop.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketOriginal.Id))).IsTrue();
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnRemove()
    {
        // having
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        var marketHigh = new Market(2);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);
        _marketList.Add(marketHigh);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        marketHigh.SetPrices(0, PricesPerMarket, HighestPrice);

        // when
        _marketList.Remove(marketLow);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(results.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(results.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketOriginal.Id))).IsTrue();
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketOriginal.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketHigh.Id))).IsTrue();
    }

    [Test]
    public async Task ComparerUpdatesToCorrectValueOnUpdate()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketFlipFlop = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketFlipFlop.SetPrices(0, PricesPerMarket, HighestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketFlipFlop);

        // when
        marketFlipFlop.UpdateAllPrices(LowestPrice);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketFlipFlop.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketOriginal.Id))).IsTrue();
    }

    [Test]
    public async Task ComparerOnlyUpdatesVisibleValuesOnUpdate()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);

        // when
        marketLow.UpdateAllPrices(LowestPrice - 1);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket * 2);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketLow.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketOriginal.Id))).IsTrue();
    }

    [Test]
    public async Task ComparerOnlyRefreshesVisibleValues()
    {
        // having
        using var highPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.HighPriceCompare).AsAggregator();
        using var lowPriceResults = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer, MarketPrice.LowPriceCompare).AsAggregator();
        var marketOriginal = new Market(0);
        var marketLow = new Market(1);
        marketOriginal.SetPrices(0, PricesPerMarket, GetRandomPrice);
        marketLow.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketList.Add(marketOriginal);
        _marketList.Add(marketLow);

        // when
        marketLow.RefreshAllPrices(LowestPrice - 1);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(2);
        await Assert.That(lowPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(lowPriceResults.Summary.Overall.Updates).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Summary.Overall.Refreshes).IsEqualTo(PricesPerMarket);
        await Assert.That(lowPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketLow.Id))).IsTrue();
        await Assert.That(highPriceResults.Data.Count).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Adds).IsEqualTo(PricesPerMarket);
        await Assert.That(highPriceResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Updates).IsEqualTo(0);
        await Assert.That(highPriceResults.Summary.Overall.Refreshes).IsEqualTo(0);
        await Assert.That(highPriceResults.Data.Items.Select(cp => cp.MarketId).All(guid => guid.Equals(marketOriginal.Id))).IsTrue();
    }

    [Test]
    public async Task EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = new Market(0);
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices, MarketPrice.EqualityComparer).AsAggregator();
        market.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketList.Add(market);

        // when
        market.UpdateAllPrices(LowestPrice);

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(1);
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
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

        // when
        using var results = _marketList.Connect().MergeManyChangeSets(m => m.LatestPrices).AsAggregator();
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
        _marketList.AddRange(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket, completable: completeChildren)));
        var hasSourceSequenceCompleted = false;
        var hasMergedSequenceCompleted = false;

        using var cleanup = _marketList.Connect().Do(_ => { }, () => hasSourceSequenceCompleted = true)
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
        _marketList.AddRange(markets);
        var receivedError = default(Exception);
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<IMarket>>(expectedError);

        using var cleanup = _marketList.Connect().Concat(throwObservable)
                            .MergeManyChangeSets(m => m.LatestPrices).Subscribe(_ => { }, err => receivedError = err);

        // when
        DisposeMarkets();

        // then
        await Assert.That(receivedError).IsEqualTo(expectedError);
    }

    [Test]
    public async Task SourceListMergeCacheChangeSets()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var changeSetList = _marketList.Connect().Transform(m => m.LatestPrices).AsObservableList();
        using var results = changeSetList.MergeChangeSets(MarketPrice.EqualityComparer).AsAggregator();
        _marketList.AddRange(markets);
        markets.ForEach((m, index) => m.AddUniquePrices(index, PricesPerMarket, ItemIdStride, GetRandomPrice));

        // when
        markets.ForEach(m => _marketList.Remove(m));

        // then
        await Assert.That(_marketListResults.Data.Count).IsEqualTo(0);
        await Assert.That(markets.Sum(m => m.PricesCache.Count)).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Data.Count).IsEqualTo(0);
        await Assert.That(results.Messages.Count).IsEqualTo(MarketCount * 2);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Removes).IsEqualTo(MarketCount * PricesPerMarket);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(0);
    }

    public void Dispose()
    {
        _marketListResults.Dispose();
        DisposeMarkets();
        _marketList.Dispose();
    }

    private async Task CheckResultContents(ChangeSetAggregator<IMarket> marketResults, ChangeSetAggregator<MarketPrice, int> priceResults)
    {
        var expectedMarkets = _marketList.Items.ToList();
        var expectedPrices = expectedMarkets.SelectMany(market => ((Market)market).PricesCache.Items).ToList();

        // These should be subsets of each other
        await Assert.That(expectedMarkets.Except(marketResults.Data.Items).Any()).IsFalse();
        await Assert.That(marketResults.Data.Items.Count).IsEqualTo(expectedMarkets.Count);

        // These should be subsets of each other
        await Assert.That(expectedPrices.Except(priceResults.Data.Items).Any()).IsFalse();
        await Assert.That(priceResults.Data.Items.Count).IsEqualTo(expectedPrices.Count);
    }

    private async Task WaitForResultContentsAsync(ChangeSetAggregator<IMarket> marketResults, ChangeSetAggregator<MarketPrice, int> priceResults, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!ResultContentsMatch(marketResults, priceResults) && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }

    private bool ResultContentsMatch(ChangeSetAggregator<IMarket> marketResults, ChangeSetAggregator<MarketPrice, int> priceResults)
    {
        var expectedMarkets = _marketList.Items.ToList();
        var expectedPrices = expectedMarkets.SelectMany(market => ((Market)market).PricesCache.Items).ToList();
        var actualMarkets = marketResults.Data.Items;
        var actualPrices = priceResults.Data.Items;

        return actualMarkets.Count == expectedMarkets.Count &&
            actualPrices.Count == expectedPrices.Count &&
            expectedMarkets.All(actualMarkets.Contains) &&
            expectedPrices.All(actualPrices.Contains);
    }

    private void DisposeMarkets()
    {
        var markets = _marketList.Items.ToArray();
        markets.ForEach(m => (m as IDisposable)?.Dispose());
        _marketList.Dispose();
    }

    private decimal GetRandomPrice() => MarketPrice.RandomPrice(_randomizer, BasePrice, PriceOffset);
}
