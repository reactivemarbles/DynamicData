using System.Collections;
using System.Collections.Generic;
namespace DynamicData.Aggregation
{
    class ReadOnlyCollection<T>: IReadOnlyCollection<T>
    {
        private readonly IEnumerable<T> _items;

        public ReadOnlyCollection(IEnumerable<T> items, int count)
        {
            _items = items;
            Count = count;
        }

        public IEnumerator<T> GetEnumerator()
        {
           return  _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count { get; }
    }
}
