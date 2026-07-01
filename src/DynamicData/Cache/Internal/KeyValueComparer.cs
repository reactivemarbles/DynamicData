// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the KeyValueComparer class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="comparer">The comparer value.</param>
internal sealed class KeyValueComparer<TObject, TKey>(IComparer<TObject>? comparer = null) : IComparer<KeyValuePair<TKey, TObject>>
{
    /// <summary>
    /// Executes the Compare operation.
    /// </summary>
    /// <param name="x">The x value.</param>
    /// <param name="y">The y value.</param>
    /// <returns>The result of the operation.</returns>
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

        if (x.Key is IComparable<TKey> xComp)
        {
            return xComp.CompareTo(y.Key);
        }

        return x.Key.GetHashCode().CompareTo(y.Key.GetHashCode());
    }
}
