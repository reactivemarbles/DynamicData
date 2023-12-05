// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;
using DynamicData.Operators;

// ReSharper disable once CheckNamespace
namespace DynamicData;

internal sealed class PagedChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IPagedChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public static new readonly IPagedChangeSet<TObject, TKey> Empty = new PagedChangeSet<TObject, TKey>();

    public PagedChangeSet(IKeyValueCollection<TObject, TKey> sortedItems, IEnumerable<Change<TObject, TKey>> updates, IPageResponse response)
        : base(updates)
    {
        Response = response;
        SortedItems = sortedItems;
    }

    private PagedChangeSet()
    {
        SortedItems = new KeyValueCollection<TObject, TKey>();
        Response = new PageResponse(0, 0, 0, 0);
    }

    public IPageResponse Response { get; }

    public IKeyValueCollection<TObject, TKey> SortedItems { get; }

    /// <summary>
    /// Determines if the two values equal each other.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>If the page change set equals the other.</returns>
    public bool Equals(PagedChangeSet<TObject, TKey> other) => SortedItems.SequenceEqual(other.SortedItems);

    public override bool Equals(object? obj) => obj is PagedChangeSet<TObject, TKey> value && Equals(value);

    public override int GetHashCode() => SortedItems.GetHashCode();

    /// <summary>
    /// Returns a <see cref="string"/> that represents the SortedItems <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the SortedItems <see cref="object"/>.
    /// </returns>
    public override string ToString() => $"{base.ToString()}, Response: {Response}, SortedItems: {SortedItems}";
}
