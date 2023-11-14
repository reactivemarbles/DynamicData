// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class AnonymousQuery<TObject, TKey>(Cache<TObject, TKey> cache) : IQuery<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Cache<TObject, TKey> _cache = cache.Clone();

    public int Count => _cache.Count;

    public IEnumerable<TObject> Items => _cache.Items;

    public IEnumerable<TKey> Keys => _cache.Keys;

    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

    public Optional<TObject> Lookup(TKey key) => _cache.Lookup(key);
}
