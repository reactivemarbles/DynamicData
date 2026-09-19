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
/// Provides members for the VirtualChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class VirtualChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IVirtualChangeSet<TObject, TKey>, IEquatable<VirtualChangeSet<TObject, TKey>>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The Empty field.
    /// </summary>
    public static new readonly IVirtualChangeSet<TObject, TKey> Empty = new VirtualChangeSet<TObject, TKey>();

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualChangeSet{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <param name="sortedItems">The sortedItems value.</param>
    /// <param name="response">The response value.</param>
    public VirtualChangeSet(IEnumerable<Change<TObject, TKey>> items, IKeyValueCollection<TObject, TKey> sortedItems, IVirtualResponse response)
        : base(items)
    {
        SortedItems = sortedItems;
        Response = response;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualChangeSet{TObject, TKey}"/> class.
    /// </summary>
    private VirtualChangeSet()
    {
        SortedItems = new KeyValueCollection<TObject, TKey>();
        Response = new VirtualResponse(0, 0, 0);
    }

    /// <summary>
    /// Gets the Response value.
    /// </summary>
    public IVirtualResponse Response { get; }

    /// <summary>
    /// Gets the SortedItems value.
    /// </summary>
    public IKeyValueCollection<TObject, TKey> SortedItems { get; }

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator ==(VirtualChangeSet<TObject, TKey> left, VirtualChangeSet<TObject, TKey> right) => Equals(left, right);

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator !=(VirtualChangeSet<TObject, TKey> left, VirtualChangeSet<TObject, TKey> right) => !Equals(left, right);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(VirtualChangeSet<TObject, TKey>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Response.Equals(other.Response) && Equals(SortedItems, other.SortedItems);
    }

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj) => obj is VirtualChangeSet<TObject, TKey> item && Equals(item);

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Response.GetHashCode();
            hashCode = (hashCode * 397) ^ SortedItems.GetHashCode();
            return hashCode;
        }
    }
}
