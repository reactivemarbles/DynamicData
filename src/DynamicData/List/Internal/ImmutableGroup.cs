// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the ImmutableGroup class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
internal sealed class ImmutableGroup<TObject, TGroupKey> : IGrouping<TObject, TGroupKey>, IEquatable<ImmutableGroup<TObject, TGroupKey>>
{
    /// <summary>
    /// The _items field.
    /// </summary>
    private readonly IReadOnlyCollection<TObject> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmutableGroup{TObject, TGroupKey}"/> class.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <param name="items">The items value.</param>
    internal ImmutableGroup(TGroupKey key, IList<TObject> items)
    {
        Key = key;
        _items = new ReadOnlyCollectionLight<TObject>(items);
    }

    /// <summary>
    /// Gets the Count value.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the Items value.
    /// </summary>
    public IEnumerable<TObject> Items => _items;

    /// <summary>
    /// Gets the Key value.
    /// </summary>
    public TGroupKey Key { get; }

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator ==(ImmutableGroup<TObject, TGroupKey> left, ImmutableGroup<TObject, TGroupKey> right) => Equals(left, right);

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator !=(ImmutableGroup<TObject, TGroupKey> left, ImmutableGroup<TObject, TGroupKey> right) => !Equals(left, right);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(ImmutableGroup<TObject, TGroupKey>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EqualityComparer<TGroupKey>.Default.Equals(Key, other.Key);
    }

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ImmutableGroup<TObject, TGroupKey> value && Equals(value);
    }

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode() => Key is null ? 0 : EqualityComparer<TGroupKey>.Default.GetHashCode(Key);

    /// <summary>
    /// Executes the ToString operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"Grouping for: {Key} ({Count} items)";
}
