using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;

using Xunit;

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

    [Theory]
    [InlineData(5, 7)]
    [InlineData(10, 50)]
#if false && !DEBUG
    [InlineData(100, 100)]
    [InlineData(10, 1_000)]
    [InlineData(1_000, 10)]
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

        var merged = _marketCache.Connect().MergeManyChangeSets(market => market.LatestPrices, Market.RatingCompare, resortOnSourceRefresh: true);
        var adding = true;
        using var priceResults = merged.AsAggregator();

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
        CheckResultContents(_marketCacheResults, priceResults, Market.RatingCompare);
    }

    [Fact]
    public void NullChecks()
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
        emptyChangeSetObs.Should().NotBeNull();
        emptyChildChangeSetObs.Should().NotBeNull();
        emptyChildComparer.Should().NotBeNull();
        emptyEqualityComparer.Should().NotBeNull();
        emptyKeySelector.Should().NotBeNull();
        emptyParentComparer.Should().NotBeNull();
        emptySelector.Should().NotBeNull();
        nullChangeSetObs.Should().BeNull();
        nullChildComparer.Should().BeNull();
        nullEqualityComparer.Should().BeNull();
        nullKeySelector.Should().BeNull();
        nullParentComparer.Should().BeNull();
        nullSelector.Should().BeNull();

        actionParentCompare1.Should().Throw<ArgumentNullException>();
        actionParentCompareKey1a.Should().Throw<ArgumentNullException>();
        actionParentCompareKey1b.Should().Throw<ArgumentNullException>();
        actionParentCompareKey1c.Should().Throw<ArgumentNullException>();
        actionParentCompare2.Should().Throw<ArgumentNullException>();
        actionParentCompareKey2a.Should().Throw<ArgumentNullException>();
        actionParentCompareKey2b.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AbleToInvokeFactory()
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
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
    }

    [Fact]
    public void AbleToInvokeFactoryWithKey()
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
        _marketCacheResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
    }

    [Fact]
    public void AllExistingSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        _marketCache.AddOrUpdate(markets);

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        markets.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(1);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void AllNewSubItemsPresentInResult()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        _marketCache.AddOrUpdate(markets);

        // when
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        markets.Sum(m => m.PricesCache.Count).Should().Be(MarketCount * PricesPerMarket);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(MarketCount);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void AllRefreshedSubItemsAreRefreshed()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));
        _marketCache.AddOrUpdate(markets);

        // when
        markets.ForEach(m => m.RefreshAllPrices(GetRandomPrice));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        results.Data.Count.Should().Be(MarketCount * PricesPerMarket);
        results.Messages.Count.Should().Be(MarketCount + 1);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(MarketCount * PricesPerMarket);
    }

    [Fact]
    public void AnyDuplicateKeyValuesShouldBeHidden()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeNoOpWhenRemoved()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldBeUnhiddenWhenOtherIsRemoved()
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
        _marketCacheResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[1].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Messages.Count.Should().Be(2);
        results.Messages[1].Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void AnyDuplicateValuesShouldNotRefreshWhenHidden()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void SourceRefreshGeneratesUpdatesAsNeeded()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[1].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void SourceRefreshDoesNothingIfDisabled()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Data.Items.Zip(markets[0].PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void AnyRemovedSubItemIsRemoved()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        markets.ForEach(m => m.PricesCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount))));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount);
        results.Data.Count.Should().Be(MarketCount * (PricesPerMarket - RemoveCount));
        results.Messages.Count.Should().Be(MarketCount * 2);
        results.Messages[0].Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(MarketCount * RemoveCount);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void AnySourceItemRemovedRemovesAllSourceValues()
    {
        // having
        var markets = Enumerable.Range(0, MarketCount).Select(n => new Market(n)).ToArray();
        using var results = ChangeSetByRating().AsAggregator();
        _marketCache.AddOrUpdate(markets);
        markets.Select((m, index) => new { Market = m, Index = index }).ForEach(m => m.Market.SetPrices(m.Index * ItemIdStride, (m.Index * ItemIdStride) + PricesPerMarket, GetRandomPrice));

        // when
        _marketCache.Edit(updater => updater.RemoveKeys(updater.Keys.Take(RemoveCount)));

        // then
        _marketCacheResults.Data.Count.Should().Be(MarketCount - RemoveCount);
        results.Data.Count.Should().Be((MarketCount - RemoveCount) * PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(MarketCount * PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket * RemoveCount);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void ChangingSourceByUpdateRemovesPreviousAndAddsNewValues()
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
        _marketCacheResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket * 2);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(PricesPerMarket);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Zip(updatedMarket.PricesCache.Items).ForEach(pair => pair.First.Should().Be(pair.Second));
    }

    [Fact]
    public void ChangingSourceByUpdateRemovesPreviousAndEmitsBetterValues()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket * 3);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * 3);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Take(PricesPerMarket).Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketWorse.Id));
        results.Data.Items.Skip(PricesPerMarket).Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(updatedMarket.Id));
    }

    [Fact]
    public void UpdatesToCorrectValueOnRemove()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
    }

    [Fact]
    public void OnlyUpdatesOnDuplicateIfNewItemIsFromBetterParent()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
    }

    [Fact]
    public void BestChoiceFromDuplicatesSelectedWhenChangeSetCreated()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
    }

    [Fact]
    public void OnlyAddsBetterValuesOnSourceUpdate()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
    }

    [Fact]
    public void UpdatesToCorrectValueOnRefresh()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        _marketCacheResults.Summary.Overall.Refreshes.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
        resultsRefresh.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsRefresh.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        resultsRefresh.Summary.Overall.Removes.Should().Be(0);
        resultsRefresh.Summary.Overall.Refreshes.Should().Be(0);
        resultsRefresh.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketBetter.Id));
        resultsLowRefresh.Data.Count.Should().Be(PricesPerMarket);
        resultsLowRefresh.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLowRefresh.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        resultsLowRefresh.Summary.Overall.Removes.Should().Be(0);
        resultsLowRefresh.Summary.Overall.Refreshes.Should().Be(0);
        resultsLowRefresh.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
    }

    [Fact]
    public void ChildComparerUpdatesToCorrectValueOnUpdate()
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
        _marketCacheResults.Data.Count.Should().Be(3);
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsLow.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));

        resultsLowPrice.Data.Count.Should().Be(PricesPerMarket);
        resultsLowPrice.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLowPrice.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
        resultsLowPrice.Summary.Overall.Removes.Should().Be(0);
        resultsLowPrice.Summary.Overall.Refreshes.Should().Be(0);
        resultsLowPrice.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLowest.Id));

        resultsHighPrice.Data.Count.Should().Be(PricesPerMarket);
        resultsHighPrice.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsHighPrice.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        resultsHighPrice.Summary.Overall.Removes.Should().Be(0);
        resultsHighPrice.Summary.Overall.Refreshes.Should().Be(0);
        resultsHighPrice.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketHighest.Id));
    }

    [Fact]
    public void ChildComparerOnlyUpdatesVisibleValuesOnUpdate()
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
        _marketCacheResults.Data.Count.Should().Be(3);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
        lowRatingLowPriceResults.Data.Count.Should().Be(PricesPerMarket);
        lowRatingLowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        lowRatingLowPriceResults.Summary.Overall.Removes.Should().Be(0);
        lowRatingLowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        lowRatingLowPriceResults.Summary.Overall.Refreshes.Should().Be(0);
        lowRatingLowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLowest.Id));
        lowRatingHighPriceResults.Data.Count.Should().Be(PricesPerMarket);
        lowRatingHighPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        lowRatingHighPriceResults.Summary.Overall.Removes.Should().Be(0);
        lowRatingHighPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
        lowRatingHighPriceResults.Summary.Overall.Refreshes.Should().Be(0);
        lowRatingHighPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
    }

    [Fact]
    public void ChildComparerOnlyRefreshesVisibleValues()
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
        _marketCacheResults.Data.Count.Should().Be(3);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
        results.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketOriginal.Id));
        lowRatingLowPriceResults.Data.Count.Should().Be(PricesPerMarket);
        lowRatingLowPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        lowRatingLowPriceResults.Summary.Overall.Removes.Should().Be(0);
        lowRatingLowPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        lowRatingLowPriceResults.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
        lowRatingLowPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLowest.Id));
        lowRatingHighPriceResults.Data.Count.Should().Be(PricesPerMarket);
        lowRatingHighPriceResults.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        lowRatingHighPriceResults.Summary.Overall.Removes.Should().Be(0);
        lowRatingHighPriceResults.Summary.Overall.Updates.Should().Be(PricesPerMarket);
        lowRatingHighPriceResults.Summary.Overall.Refreshes.Should().Be(0);
        lowRatingHighPriceResults.Data.Items.Select(cp => cp.MarketId).ForEach(guid => guid.Should().Be(marketLow.Id));
    }

    [Fact]
    public void EqualityComparerHidesUpdatesWithoutChanges()
    {
        // having
        var market = new Market(0);
        using var results = CreateChangeSet("Equality Compare", Market.RatingCompare, equalityComparer: MarketPrice.EqualityComparer, resortOnRefresh: true).AsAggregator();
        market.SetPrices(0, PricesPerMarket, LowestPrice);
        _marketCache.AddOrUpdate(market);

        // when
        market.SetPrices(0, PricesPerMarket, LowestPrice);

        // then
        _marketCacheResults.Data.Count.Should().Be(1);
        results.Data.Count.Should().Be(PricesPerMarket);
        results.Messages.Count.Should().Be(1);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void EqualityComparerAndChildComparerWorkTogetherForUpdates()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Messages.Count.Should().Be(1);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsRecent.Messages.Count.Should().Be(3);
        resultsRecent.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsRecent.Summary.Overall.Removes.Should().Be(0);
        resultsRecent.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        resultsRecent.Summary.Overall.Refreshes.Should().Be(0);
        resultsTimeStamp.Messages.Count.Should().Be(4);
        resultsTimeStamp.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsTimeStamp.Summary.Overall.Removes.Should().Be(0);
        resultsTimeStamp.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
        resultsTimeStamp.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Fact]
    public void EqualityComparerAndChildComparerWorkTogetherForRefreshes()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Messages.Count.Should().Be(1);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsRecent.Messages.Count.Should().Be(3);
        resultsRecent.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsRecent.Summary.Overall.Removes.Should().Be(0);
        resultsRecent.Summary.Overall.Updates.Should().Be(PricesPerMarket * 2);
        resultsRecent.Summary.Overall.Refreshes.Should().Be(0);
        resultsTimeStamp.Messages.Count.Should().Be(5);
        resultsTimeStamp.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsTimeStamp.Summary.Overall.Removes.Should().Be(0);
        resultsTimeStamp.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
        resultsTimeStamp.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void EqualityComparerAndChildComparerRefreshesBecomeUpdates()
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
        _marketCacheResults.Data.Count.Should().Be(2);
        resultsLow.Data.Count.Should().Be(PricesPerMarket);
        resultsLow.Messages.Count.Should().Be(1);
        resultsLow.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsLow.Summary.Overall.Removes.Should().Be(0);
        resultsLow.Summary.Overall.Updates.Should().Be(0);
        resultsLow.Summary.Overall.Refreshes.Should().Be(0);
        resultsRecent.Messages.Count.Should().Be(4);
        resultsRecent.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsRecent.Summary.Overall.Removes.Should().Be(0);
        resultsRecent.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
        resultsRecent.Summary.Overall.Refreshes.Should().Be(0);
        resultsTimeStamp.Messages.Count.Should().Be(5);
        resultsTimeStamp.Summary.Overall.Adds.Should().Be(PricesPerMarket);
        resultsTimeStamp.Summary.Overall.Removes.Should().Be(0);
        resultsTimeStamp.Summary.Overall.Updates.Should().Be(PricesPerMarket * 3);
        resultsTimeStamp.Summary.Overall.Refreshes.Should().Be(PricesPerMarket);
    }

    [Fact]
    public void EveryItemVisibleWhenSequenceCompletes()
    {
        // having
        _marketCache.AddOrUpdate(Enumerable.Range(0, MarketCount).Select(n => new FixedMarket(GetRandomPrice, n * ItemIdStride, (n * ItemIdStride) + PricesPerMarket)));

        // when
        using var results = ChangeSetByRating(false).AsAggregator();
        DisposeMarkets();

        // then
        results.Data.Count.Should().Be(PricesPerMarket * MarketCount);
        results.Summary.Overall.Adds.Should().Be(PricesPerMarket * MarketCount);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
        results.Summary.Overall.Refreshes.Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MergedObservableCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
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
        hasSourceSequenceCompleted.Should().Be(completeSource);
        hasMergedSequenceCompleted.Should().Be(completeSource && completeChildren);
    }

    [Fact]
    public void MergedObservableWillFailIfSourceFails()
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
        receivedError.Should().Be(expectedError);
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
        DisposeMarkets();
    }

    private void CheckResultContents(ChangeSetAggregator<IMarket, Guid> marketResults, ChangeSetAggregator<MarketPrice, int> priceResults, IComparer<IMarket> comparer)
    {
        var expectedMarkets = _marketCache.Items.ToList();

        // These should be subsets of each other
        expectedMarkets.Should().BeSubsetOf(marketResults.Data.Items);
        marketResults.Data.Items.Count.Should().Be(expectedMarkets.Count);

        // Pair up all the Markets/Prices, Group them by ItemId, and sort each Group by the Market comparer
        // Then pull out the first value from each group, which should be the price from the best market for each ItemId
        var expectedPrices = expectedMarkets.Select(m => (Market)m).SelectMany(m => m.PricesCache.Items.Select(mp => (Market: m, MarketPrice: mp)))
            .GroupBy(tuple => tuple.MarketPrice.ItemId)
            .Select(group => group.OrderBy(tuple => tuple.Market, comparer).Select(tuple => tuple.MarketPrice).First())
            .ToList();

        // These should be subsets of each other
        expectedPrices.Should().BeSubsetOf(priceResults.Data.Items);
        priceResults.Data.Items.Count.Should().Be(expectedPrices.Count);
    }

    private void DisposeMarkets()
    {
        _marketCache.Items.ForEach(m => (m as IDisposable)?.Dispose());
        _marketCache.Dispose();
        _marketCache.Clear();
    }

    private decimal GetRandomPrice() => MarketPrice.RandomPrice(_randomizer, BasePrice, PriceOffset);
}
