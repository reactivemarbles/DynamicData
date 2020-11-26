// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal class KeyValueComparer<TObject, TKey> : IComparer<KeyValuePair<TKey, TObject>>
    {
        private readonly IComparer<TObject>? _comparer;

        public KeyValueComparer(IComparer<TObject>? comparer = null)
        {
            _comparer = comparer;
        }

        public int Compare(KeyValuePair<TKey, TObject> x, KeyValuePair<TKey, TObject> y)
        {
            if (_comparer is not null)
            {
                int result = _comparer.Compare(x.Value, y.Value);

                if (result != 0)
                {
                    return result;
                }
            }

            if (x.Key is null && y.Key is null)
            {
                return 0;
            }

            if (x.Key is null)
            {
                return 1;
            }

            if (y.Key is null)
            {
                return -1;
            }

            return x.Key.GetHashCode().CompareTo(y.Key.GetHashCode());
        }
    }
}