// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the AnonymousQuery class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="cache">The cache value.</param>
internal sealed class AnonymousQuery<TObject, TKey>(Cache<TObject, TKey> cache) : IQuery<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly Cache<TObject, TKey> _cache = cache.Clone();

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IEnumerable<TObject> Items => _cache.Items;

    /// <summary>
    /// Gets the Keys value.
    /// </summary>
    public IEnumerable<TKey> Keys => _cache.Keys;

    /// <summary>
    /// Gets the KeyValues value.
    /// </summary>
    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

    /// <summary>
    /// Executes the Lookup operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key) => _cache.Lookup(key);
}
