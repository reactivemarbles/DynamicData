// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Cache.Internal;

internal static class DictionaryExtensions
{
    internal static IEnumerable<T> GetOrEmpty<TDictKey, T>(this IDictionary<TDictKey, IEnumerable<T>> dict, TDictKey key)
    {
        if (dict.ContainsKey(key))
        {
            return dict[key];
        }

        return Enumerable.Empty<T>();
    }
}
