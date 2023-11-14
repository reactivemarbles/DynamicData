// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal sealed class KeyComparer<TObject, TKey> : IEqualityComparer<KeyValuePair<TKey, TObject>>
{
    public bool Equals(KeyValuePair<TKey, TObject> x, KeyValuePair<TKey, TObject> y) => x.Key?.Equals(y.Key) ?? false;

    public int GetHashCode(KeyValuePair<TKey, TObject> obj) => obj.Key is null ? 0 : obj.Key.GetHashCode();
}
