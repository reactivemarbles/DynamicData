// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the DictionaryExtensions class.
/// </summary>
internal static class DictionaryExtensions
{
    /// <summary>
    /// Executes the GetOrEmpty operation.
    /// </summary>
    /// <typeparam name="TDictKey">The type of the TDictKey value.</typeparam>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="dict">The dict value.</param>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    internal static IEnumerable<T> GetOrEmpty<TDictKey, T>(this IDictionary<TDictKey, IEnumerable<T>> dict, TDictKey key) =>
        dict.TryGetValue(key, out var value) ? value : [];
}
