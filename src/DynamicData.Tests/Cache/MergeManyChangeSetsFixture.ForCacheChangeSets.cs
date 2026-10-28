namespace DynamicData.Tests.Cache;

public static partial class MergeManyChangeSetsFixture
{
    public static partial class ForCacheChangeSets
    {
        #if DEBUG
        private const int MarketCount = 5;
        private const int PricesPerMarket = 7;
        private const int RemoveCount = 3;
        #else
        private const int MarketCount = 101;
        private const int PricesPerMarket = 103;
        private const int RemoveCount = 53;
        #endif

        private const int ItemIdStride = 1000;
        private const decimal BasePrice = 10m;
        private const decimal PriceOffset = 10m;
        private const decimal HighestPrice = BasePrice + PriceOffset + 1.0m;
        private const decimal LowestPrice = BasePrice - 1.0m;

        private static decimal GetRandomPrice(Randomizer randomizer)
            => MarketPrice.RandomPrice(randomizer, BasePrice, PriceOffset);

    }
}
