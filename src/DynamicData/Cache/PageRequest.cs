// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Represents a new page request.
/// </summary>
public sealed class PageRequest : IPageRequest, IEquatable<IPageRequest>
{
    /// <summary>
    /// The default page request.
    /// </summary>
    public static readonly IPageRequest Default = new PageRequest();

    /// <summary>
    /// Represents an empty page.
    /// </summary>
    public static readonly IPageRequest Empty = new PageRequest(0, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="PageRequest"/> class.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="size">The size.</param>
    public PageRequest(int page, int size)
    {
        if (page < 0)
        {
            throw new ArgumentException("Page must be positive");
        }

        if (size < 0)
        {
            throw new ArgumentException("Size must be positive");
        }

        Page = page;
        Size = size;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PageRequest"/> class.
    /// </summary>
    public PageRequest()
    {
    }

    /// <summary>
    /// Gets the default comparer.
    /// </summary>
    /// <value>
    /// The default comparer.
    /// </value>
    public static IEqualityComparer<IPageRequest?> DefaultComparer { get; } = new PageSizeEqualityComparer();

    /// <summary>
    /// Gets the page to move to.
    /// </summary>
    public int Page { get; } = 1;

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int Size { get; } = 25;

    /// <inheritdoc />
    public bool Equals(IPageRequest? other) => DefaultComparer.Equals(this, other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IPageRequest value && Equals(value);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Size * 397) ^ Page;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"Page: {Page}, Size: {Size}";

    private sealed class PageSizeEqualityComparer : IEqualityComparer<IPageRequest?>
    {
        public bool Equals(IPageRequest? x, IPageRequest? y)
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

            return x.Page == y.Page && x.Size == y.Size;
        }

        public int GetHashCode(IPageRequest? obj)
        {
            if (obj is null)
            {
                return 0;
            }

            unchecked
            {
                return (obj.Page * 397) ^ obj.Size;
            }
        }
    }
}
