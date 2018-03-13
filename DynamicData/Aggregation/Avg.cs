 
namespace DynamicData.Aggregation
{
    internal readonly struct Avg<TValue>
    {
        public Avg(int count, TValue sum)
        {
            Count = count;
            Sum = sum;
        }

        public int Count { get; }
        public TValue Sum { get; }
    }
}
