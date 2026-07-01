// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ImmutableGroup class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
internal sealed class ImmutableGroup<TObject, TKey, TGroupKey> : IGrouping<TObject, TKey, TGroupKey>, IEquatable<ImmutableGroup<TObject, TKey, TGroupKey>>
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly Cache<TObject, TKey> _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmutableGroup{TObject, TKey, TGroupKey}"/> class.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="cache">The cache value.</param>
    internal ImmutableGroup(TGroupKey key, ICache<TObject, TKey> cache)
    {
        Key = key;
        _cache = new Cache<TObject, TKey>(cache.Count);
        cache.KeyValues.ForEach(kvp => _cache.AddOrUpdate(kvp.Value, kvp.Key));
    }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IEnumerable<TObject> Items => _cache.Items;

    /// <summary>
    /// Gets the Key value.
    /// </summary>
    public TGroupKey Key { get; }

    /// <summary>
    /// Gets the Keys value.
    /// </summary>
    public IEnumerable<TKey> Keys => _cache.Keys;

    /// <summary>
    /// Gets the KeyValues value.
    /// </summary>
    public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator ==(ImmutableGroup<TObject, TKey, TGroupKey> left, ImmutableGroup<TObject, TKey, TGroupKey> right) => Equals(left, right);

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator !=(ImmutableGroup<TObject, TKey, TGroupKey> left, ImmutableGroup<TObject, TKey, TGroupKey> right) => !Equals(left, right);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(ImmutableGroup<TObject, TKey, TGroupKey>? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && EqualityComparer<TGroupKey?>.Default.Equals(Key, other.Key);
    }

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj) => obj is ImmutableGroup<TObject, TKey, TGroupKey> value && Equals(value);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode() => EqualityComparer<TGroupKey>.Default.GetHashCode(Key);

    /// <summary>
    /// Executes the Lookup operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    public ReactiveUI.Primitives.Optional<TObject> Lookup(TKey key) => _cache.Lookup(key);

    /// <summary>
    /// Executes the ToString operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"Grouping for: {Key} ({Count} items)";
}
