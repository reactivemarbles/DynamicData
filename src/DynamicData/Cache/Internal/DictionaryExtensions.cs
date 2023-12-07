// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal static class DictionaryExtensions
{
    internal static IEnumerable<T> GetOrEmpty<TDictKey, T>(this IDictionary<TDictKey, IEnumerable<T>> dict, TDictKey key) =>
        dict.TryGetValue(key, out var value) ? value : Enumerable.Empty<T>();
}
