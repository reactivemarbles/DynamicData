using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Kernel;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Domain;

internal interface IMarket
{
    public string Name { get; }

    public double Rating { get; set; }

    public Guid Id { get; }

    public IObservable<IChangeSet<MarketPrice, int>> LatestPrices { get; }
}

internal sealed class Market : IMarket, IDisposable
{
    private static int s_UniquePriceId;

    private readonly ISourceCache<MarketPrice, int> _latestPrices = new SourceCache<MarketPrice, int>(p => p.ItemId);

    public static IComparer<IMarket> RatingCompare { get; } = new RatingComparer();

    private Market(string name, Guid id)
    {
        Name = name;
        Id = id;
    }

    public Market(Market market) : this(market.Name, market.Id)
    {
    }

    public Market(int name) : this($"Market #{name}", Guid.NewGuid())
    {
    }

    public Market(string name) : this(name, Guid.NewGuid())
    {
    }

    public string Name { get; }

    public Guid Id { get; }

    public double Rating { get; set; }

    public IObservable<IChangeSet<MarketPrice, int>> LatestPrices => _latestPrices.Connect();

    public ISourceCache<MarketPrice, int> PricesCache => _latestPrices;

    public MarketPrice CreatePrice(int itemId, decimal price) => new(itemId, price, Id);

    public MarketPrice CreateUniquePrice(Func<int, decimal> getPrice)
    {
        var id = Interlocked.Increment(ref s_UniquePriceId);
        return CreatePrice(id, getPrice(id));
    }

    public Market AddRandomIdPrices(Random r, int count, int minId, int maxId, Func<decimal> randPrices)
    {
        _latestPrices.AddOrUpdate(Enumerable.Range(0, int.MaxValue).Select(_ => r.Next(minId, maxId)).Distinct().Take(count).Select(id => CreatePrice(id, randPrices())));
        return this;
    }

    public Market AddUniquePrices(int section, int count, int stride, Func<decimal> getPrice) => SetPrices(section * stride, section * stride + count, getPrice);

    public Market RefreshPrice(int id, decimal newPrice) => this.With(_ =>
        _latestPrices.Edit(updater => updater.Lookup(id).IfHasValue(cp =>
        {
            cp.Price = newPrice;
            updater.Refresh(cp);
        })));

    public Market RefreshAllPrices(Func<int, decimal> getNewPrice) => this.With(_ =>
        _latestPrices.Edit(updater => updater.Items.ForEach(cp =>
        {
            cp.Price = getNewPrice(cp.ItemId);
            updater.Refresh(cp);
        })));

    public Market RefreshAllPrices(Func<decimal> getNewPrice) => RefreshAllPrices(_ => getNewPrice());
    
    public Market RefreshAllPrices(decimal newPrice) => RefreshAllPrices(_ => newPrice);

    public void RemoveAllPrices() => this.With(_ => _latestPrices.Clear());

    public void RemovePrice(int itemId) => this.With(_ => _latestPrices.Remove(itemId));

    public Market UpdateAllPrices(Func<int, decimal> getNewPrice) => this.With(_ =>
        _latestPrices.Edit(updater => updater.AddOrUpdate(updater.Items.Select(cp => CreatePrice(cp.ItemId, getNewPrice(cp.ItemId))))));

    public Market UpdateAllPrices(Func<decimal> getNewPrice) => UpdateAllPrices(_ => getNewPrice());

    public Market UpdateAllPrices(decimal newPrice) => UpdateAllPrices(_ => newPrice);

    public Market SetPrices(int minId, int maxId, Func<int, decimal> getPrice) => this.With(_ =>
        _latestPrices.AddOrUpdate(Enumerable.Range(minId, maxId - minId).Select(id => CreatePrice(id, getPrice(id)))));

    public Market SetPrices(int minId, int maxId, Func<decimal> getPrice) => SetPrices(minId, maxId, i => getPrice());

    public Market SetPrices(int minId, int maxId, decimal newPrice) => SetPrices(minId, maxId, _ => newPrice);

    public Market SetPrice(int id, Func<decimal> getPrice) => this.With(_ => _latestPrices.AddOrUpdate(CreatePrice(id, getPrice())));

    public Market AddUniquePrices(int count, Func<int, decimal> getPrice) =>
        this.With(_ => _latestPrices.AddOrUpdate(CreateUniquePrices(count, getPrice)));

    public void Dispose() => _latestPrices.Dispose();

    public override string ToString() => $"Market '{Name}' [{Id}] (Rating: {Rating})";

    private IEnumerable<MarketPrice> CreateUniquePrices(int count, Func<int, decimal> getPrice) =>
        Enumerable.Range(0, count).Select(_ => CreateUniquePrice(getPrice));

    private class RatingComparer : IComparer<IMarket>
    {
        public int Compare([DisallowNull] IMarket x, [DisallowNull] IMarket y) =>
            // Higher ratings go first
            y.Rating.CompareTo(x.Rating);
    }
}


internal sealed class FixedMarket : IMarket
{
    public FixedMarket(Func<decimal> getPrice, int minId, int maxId, bool completable = true)
    {
        Id = Guid.NewGuid();
        LatestPrices = Enumerable.Range(minId, maxId - minId)
                                .Select(id => new MarketPrice(id, getPrice(), Id))
                                .AsObservableChangeSet(cp => cp.ItemId, completable: completable);
    }

    public IObservable<IChangeSet<MarketPrice, int>> LatestPrices { get; }

    public string Name => Id.ToString("B");

    public double Rating { get; set; }

    public Guid Id { get; }

    public override string ToString() => $"Fixed Market '{Name}' (Rating: {Rating})";
}
