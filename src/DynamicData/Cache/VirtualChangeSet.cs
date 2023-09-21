// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

internal sealed class VirtualChangeSet<TObject, TKey> : ChangeSet<TObject, TKey>, IVirtualChangeSet<TObject, TKey>, IEquatable<VirtualChangeSet<TObject, TKey>>
    where TObject : notnull
    where TKey : notnull
{
    public static readonly new IVirtualChangeSet<TObject, TKey> Empty = new VirtualChangeSet<TObject, TKey>();

    public VirtualChangeSet(IEnumerable<Change<TObject, TKey>> items, IKeyValueCollection<TObject, TKey> sortedItems, IVirtualResponse response)
        : base(items)
    {
        SortedItems = sortedItems;
        Response = response;
    }

    private VirtualChangeSet()
    {
        SortedItems = new KeyValueCollection<TObject, TKey>();
        Response = new VirtualResponse(0, 0, 0);
    }

    public IVirtualResponse Response { get; }

    public IKeyValueCollection<TObject, TKey> SortedItems { get; }

    public static bool operator ==(VirtualChangeSet<TObject, TKey> left, VirtualChangeSet<TObject, TKey> right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(VirtualChangeSet<TObject, TKey> left, VirtualChangeSet<TObject, TKey> right)
    {
        return !Equals(left, right);
    }

    public bool Equals(VirtualChangeSet<TObject, TKey>? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Response.Equals(other.Response) && Equals(SortedItems, other.SortedItems);
    }

    public override bool Equals(object? obj)
    {
        return obj is VirtualChangeSet<TObject, TKey> item && Equals(item);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Response.GetHashCode();
            hashCode = (hashCode * 397) ^ SortedItems.GetHashCode();
            return hashCode;
        }
    }
}
