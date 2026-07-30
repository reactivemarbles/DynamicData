// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the KeyComparer class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class KeyComparer<TObject, TKey> : IEqualityComparer<KeyValuePair<TKey, TObject>>
{
    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="x">The x value.</param>
    /// <param name="y">The y value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(KeyValuePair<TKey, TObject> x, KeyValuePair<TKey, TObject> y) => x.Key?.Equals(y.Key) ?? false;

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetHashCode(KeyValuePair<TKey, TObject> obj) => obj.Key is null ? 0 : obj.Key.GetHashCode();
}
