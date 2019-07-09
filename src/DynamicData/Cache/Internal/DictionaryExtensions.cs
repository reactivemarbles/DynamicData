using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Cache.Internal
{
    internal static class DictionaryExtensions
    {
        internal static IEnumerable<T> GetOrEmpty<TDictKey, T>(this IDictionary<TDictKey, IEnumerable<T>> dict, TDictKey key)
        {
            if (dict.ContainsKey(key))
                return dict[key];

            return Enumerable.Empty<T>();
        }
    }
}
