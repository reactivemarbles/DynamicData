using System.Collections;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    internal sealed class ReadOnlyCollectionLight<T> : IReadOnlyCollection<T>
    {
        private readonly IEnumerable<T> _items;

        public ReadOnlyCollectionLight(IEnumerable<T> items, int count)
        {
            _items = items;
            Count = count;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count { get; }
    }
}
