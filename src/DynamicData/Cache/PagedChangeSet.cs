// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif
#if REACTIVE_SHIM
using DynamicData.Reactive.Operators;
#else
using DynamicData.Operators;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Provides members for the PagedChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class PagedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IPagedChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The Empty field.
    /// </summary>
    public static new readonly IPagedChangeSet<TObject, TKey> Empty = new PagedChangeSet<TObject, TKey>();

    /// <summary>
    /// Initializes a new instance of the <see cref="PagedChangeSet{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="sortedItems">The sortedItems value.</param>
    /// <param name="updates">The updates value.</param>
    /// <param name="response">The response value.</param>
    public PagedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates, IPageResponse response)
        : base(updates)
    {
        Response = response;
        SortedItems = sortedItems;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PagedChangeSet{TObject, TKey}"/> class.
    /// </summary>
    private PagedChangeSet()
    {
        SortedItems = new KeyValueCollection<TObject, TKey>();
        Response = new PageResponse(0, 0, 0, 0);
    }

    /// <summary>
    /// Gets the Response value.
    /// </summary>
    public IPageResponse Response { get; }

    /// <summary>
    /// Gets the SortedItems value.
    /// </summary>
    public IKeyValueCollection<TObject, TKey> SortedItems { get; }

    /// <summary>
    /// Determines if the two values equal each other.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>If the page change set equals the other.</returns>
    public bool Equals(PagedChangeSet<TObject, TKey> other) => SortedItems.SequenceEqual(other.SortedItems);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj) => obj is PagedChangeSet<TObject, TKey> value && Equals(value);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode() => SortedItems.GetHashCode();

    /// <summary>
    /// Returns a <see cref="string"/> that represents the SortedItems <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the SortedItems <see cref="object"/>.
    /// </returns>
    public override string ToString() => $"{base.ToString()}, Response: {Response}, SortedItems: {SortedItems}";
}
