
namespace DynamicData.Aggregation
{
    internal struct StdDev<TValue>
    {
        public StdDev(int count, TValue sumOfItems, TValue sumOfSquares)
        {
            Count = count;
            SumOfItems = sumOfItems;
            SumOfSquares = sumOfSquares;
        }

        public int Count { get; }

        public TValue SumOfItems { get; }

        public TValue SumOfSquares { get; }
    }
}
