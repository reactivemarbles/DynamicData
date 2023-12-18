// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal sealed class KeyValueComparer<TObject, TKey>(IComparer<TObject>? comparer = null) : IComparer<KeyValuePair<TKey, TObject>>
{
    public int Compare(KeyValuePair<TKey, TObject> x, KeyValuePair<TKey, TObject> y)
    {
        if (comparer is not null)
        {
            var result = comparer.Compare(x.Value, y.Value);

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
