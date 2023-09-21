// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Operators;

// ReSharper disable once CheckNamespace
namespace DynamicData;

internal sealed class PageResponse : IEquatable<IPageResponse>, IPageResponse
{
    public PageResponse(int pageSize, int totalSize, int page, int pages)
    {
        PageSize = pageSize;
        TotalSize = totalSize;
        Page = page;
        Pages = pages;
    }

    public static IEqualityComparer<IPageResponse?> DefaultComparer { get; } = new PageResponseEqualityComparer();

    public int Page { get; }

    public int Pages { get; }

    public int PageSize { get; }

    public int TotalSize { get; }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(IPageResponse? other)
    {
        return DefaultComparer.Equals(this, other);
    }

    /// <summary>
    /// Determines whether the specified <see cref="object"/> is equal to the current <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// true if the specified <see cref="object"/> is equal to the current <see cref="object"/>; otherwise, false.
    /// </returns>
    /// <param name="obj">The <see cref="object"/> to compare with the current <see cref="object"/>. </param>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (!(obj is IPageResponse pageResponse))
        {
            return false;
        }

        return Equals(pageResponse);
    }

    /// <summary>
    /// Serves as a hash function for a particular type.
    /// </summary>
    /// <returns>
    /// A hash code for the current <see cref="object"/>.
    /// </returns>
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Pages;
            hashCode = (hashCode * 397) ^ Page;
            hashCode = (hashCode * 397) ^ TotalSize;
            hashCode = (hashCode * 397) ^ PageSize;
            return hashCode;
        }
    }

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current <see cref="object"/>.
    /// </returns>
    public override string ToString()
    {
        return $"Page: {Page}, PageSize: {PageSize}, Pages: {Pages}, TotalSize: {TotalSize}";
    }

    private sealed class PageResponseEqualityComparer : IEqualityComparer<IPageResponse?>
    {
        public bool Equals(IPageResponse? x, IPageResponse? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.PageSize == y.PageSize && x.TotalSize == y.TotalSize && x.Page == y.Page && x.Pages == y.Pages;
        }

        public int GetHashCode(IPageResponse? obj)
        {
            if (obj is null)
            {
                return 0;
            }

            unchecked
            {
                int hashCode = obj.PageSize;
                hashCode = (hashCode * 397) ^ obj.TotalSize;
                hashCode = (hashCode * 397) ^ obj.Page;
                hashCode = (hashCode * 397) ^ obj.Pages;
                return hashCode;
            }
        }
    }
}
