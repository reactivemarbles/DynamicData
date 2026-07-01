// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Provides members for the SortedChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class SortedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, ISortedChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The Empty field.
    /// </summary>
    public static new readonly ISortedChangeSet<TObject, TKey> Empty = new SortedChangeSet<TObject, TKey>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedChangeSet{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="sortedItems">The sortedItems value.</param>
    /// <param name="updates">The updates value.</param>
    public SortedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates)
        : base(updates) => SortedItems = sortedItems;

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedChangeSet{TObject, TKey}"/> class.
    /// </summary>
    private SortedChangeSet() => SortedItems = new KeyValueCollection<TObject, TKey>();

    /// <summary>
    /// Gets the SortedItems value.
    /// </summary>
    public IKeyValueCollection<TObject, TKey> SortedItems { get; }

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(SortedChangeSet<TObject, TKey> other) => SortedItems.SequenceEqual(other.SortedItems);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj) => obj is SortedChangeSet<TObject, TKey> value && Equals(value);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode() => SortedItems.GetHashCode();

    /// <summary>
    /// Executes the ToString operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"SortedChangeSet. Count= {SortedItems.Count}. Updates = {Count}";
}
