// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
/// Provides members for the PageResponse class.
/// </summary>
/// <param name="pageSize">The pageSize value.</param>
/// <param name="totalSize">The totalSize value.</param>
/// <param name="page">The page value.</param>
/// <param name="pages">The pages value.</param>
internal sealed class PageResponse(int pageSize, int totalSize, int page, int pages) : IEquatable<IPageResponse>, IPageResponse
{
    /// <summary>
    /// Gets the DefaultComparer value.
    /// </summary>
    public static IEqualityComparer<IPageResponse?> DefaultComparer { get; } = new PageResponseEqualityComparer();

    /// <summary>
    /// Gets the Page value.
    /// </summary>
    public int Page { get; } = page;

    /// <summary>
    /// Gets the Pages value.
    /// </summary>
    public int Pages { get; } = pages;

    /// <summary>
    /// Gets the PageSize value.
    /// </summary>
    public int PageSize { get; } = pageSize;

    /// <summary>
    /// Gets the TotalSize value.
    /// </summary>
    public int TotalSize { get; } = totalSize;

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(IPageResponse? other) => DefaultComparer.Equals(this, other);

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

        if (obj is not IPageResponse pageResponse)
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
            var hashCode = Pages;
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
    public override string ToString() => $"Page: {Page}, PageSize: {PageSize}, Pages: {Pages}, TotalSize: {TotalSize}";

/// <summary>
/// Provides members for the PageResponseEqualityComparer class.
/// </summary>
private sealed class PageResponseEqualityComparer : IEqualityComparer<IPageResponse?>
    {
        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Equals(IPageResponse? x, IPageResponse? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x.PageSize == y.PageSize && x.TotalSize == y.TotalSize && x.Page == y.Page && x.Pages == y.Pages;
        }

        /// <summary>
        /// Executes the GetHashCode operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result of the operation.</returns>
        public int GetHashCode(IPageResponse? obj)
        {
            if (obj is null)
            {
                return 0;
            }

            unchecked
            {
                var hashCode = obj.PageSize;
                hashCode = (hashCode * 397) ^ obj.TotalSize;
                hashCode = (hashCode * 397) ^ obj.Page;
                hashCode = (hashCode * 397) ^ obj.Pages;
                return hashCode;
            }
        }
    }
}
