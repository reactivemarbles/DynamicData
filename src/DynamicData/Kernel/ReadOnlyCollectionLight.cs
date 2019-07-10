// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{
    internal sealed class ReadOnlyCollectionLight<T> : IReadOnlyCollection<T>
    {
        private readonly IList<T> _items;

        public static readonly IReadOnlyCollection<T> Empty = new ReadOnlyCollectionLight<T>();

        public ReadOnlyCollectionLight(IEnumerable<T> items)
        {
            _items = items.ToList();
            Count = _items.Count;
        }

        private ReadOnlyCollectionLight()
        {
            _items = new List<T>();
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
