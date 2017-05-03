using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{
    internal sealed class ReadOnlyCollectionLight<T> : IReadOnlyCollection<T>
    {
        private readonly IEnumerable<T> _items;

        public static readonly IReadOnlyCollection<T> Empty = new ReadOnlyCollectionLight<T>();

        public ReadOnlyCollectionLight(IEnumerable<T> items, int count)
        {
            _items = items;
            Count = count;
        }

        private ReadOnlyCollectionLight()
        {
            _items = Enumerable.Empty<T>();
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
