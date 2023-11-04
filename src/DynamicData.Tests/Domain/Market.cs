using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Domain;

internal interface IMarket
{
    public string Name { get; }

    public Guid Id { get; }

    public IObservable<IChangeSet<MarketPrice, int>> LatestPrices { get; }
}

internal class Market : IMarket, IDisposable
{
    private readonly ISourceCache<MarketPrice, int> _latestPrices = new SourceCache<MarketPrice, int>(p => p.ItemId);

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

    public string Name { get; }

    public Guid Id { get; }

    public IObservable<IChangeSet<MarketPrice, int>> LatestPrices => _latestPrices.Connect();

    public ISourceCache<MarketPrice, int> PricesCache => _latestPrices;

    public MarketPrice CreatePrice(int itemId, decimal price) => new(itemId, price, Id);

    public Market AddRandomIdPrices(Random r, int count, int minId, int maxId, Func<decimal> randPrices)
    {
        _latestPrices.AddOrUpdate(Enumerable.Range(0, int.MaxValue).Select(_ => r.Next(minId, maxId)).Distinct().Take(count).Select(id => CreatePrice(id, randPrices())));
        return this;
    }

    public Market AddRandomPrices(int minId, int maxId, Func<decimal> randPrices)
    {
        _latestPrices.AddOrUpdate(Enumerable.Range(minId, maxId - minId).Select(id => CreatePrice(id, randPrices())));
        return this;
    }

    public Market AddUniquePrices(int section, int count, int stride, Func<decimal> randPrices) => AddRandomPrices(section * stride, section * stride + count, randPrices);

    public Market RefreshAllPrices(decimal newPrice)
    {
        _latestPrices.Edit(updater => updater.Items.ForEach(cp =>
        {
            cp.Price = newPrice;
            updater.Refresh(cp);
        }));

        return this;
    }

    public Market RefreshAllPrices(Func<decimal> randPrices) => RefreshAllPrices(randPrices());

    public Market RefreshPrice(int id, decimal newPrice)
    {
        _latestPrices.Edit(updater => updater.Lookup(id).IfHasValue(cp =>
        {
            cp.Price = newPrice;
            updater.Refresh(cp);
        }));
        return this;
    }

    public void RemoveAllPrices() => this.With(_ => _latestPrices.Clear());

    public void RemovePrice(int itemId) => this.With(_ => _latestPrices.Remove(itemId));

    public Market UpdateAllPrices(decimal newPrice) => this.With(_ => _latestPrices.Edit(updater => updater.AddOrUpdate(updater.Items.Select(cp => CreatePrice(cp.ItemId, newPrice)))));

    public Market UpdatePrices(int minId, int maxId, decimal newPrice) => this.With(_ => _latestPrices.AddOrUpdate(Enumerable.Range(minId, maxId - minId).Select(id => CreatePrice(id, newPrice))));

    public void Dispose() => _latestPrices.Dispose();
}


internal class FixedMarket : IMarket
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

    public Guid Id { get; }
}
