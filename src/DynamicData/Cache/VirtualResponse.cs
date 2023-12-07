// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Defines values used to virtualise the result set.
/// </summary>
internal sealed class VirtualResponse(int size, int startIndex, int totalSize) : IEquatable<IVirtualResponse>, IVirtualResponse
{
    public static IEqualityComparer<IVirtualResponse?> DefaultComparer { get; } = new TotalSizeStartIndexSizeEqualityComparer();

    /// <summary>
    /// Gets the requested size of the virtualised data.
    /// </summary>
    public int Size { get; } = size;

    /// <summary>
    /// Gets the starting index.
    /// </summary>
    public int StartIndex { get; } = startIndex;

    /// <summary>
    /// Gets the total size of the underlying cache.
    /// </summary>
    public int TotalSize { get; } = totalSize;

    /// <summary>
    ///     Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <returns>
    ///     true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
    /// </returns>
    /// <param name="other">An object to compare with this object.</param>
    public bool Equals(IVirtualResponse? other) => DefaultComparer.Equals(this, other);

    /// <summary>
    ///     Determines whether the specified <see cref="object" /> is equal to the current <see cref="object" />.
    /// </summary>
    /// <returns>
    ///     true if the specified object  is equal to the current object; otherwise, false.
    /// </returns>
    /// <param name="obj">The object to compare with the current object. </param>
    public override bool Equals(object? obj) => obj is IVirtualResponse item && Equals(item);

    /// <summary>
    ///     Serves as a hash function for a particular type.
    /// </summary>
    /// <returns>
    ///     A hash code for the current <see cref="object" />.
    /// </returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Size;
            hashCode = (hashCode * 397) ^ StartIndex;
            hashCode = (hashCode * 397) ^ TotalSize;
            return hashCode;
        }
    }

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current <see cref="object"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current <see cref="object"/>.
    /// </returns>
    public override string ToString() => $"Size: {Size}, StartIndex: {StartIndex}, TotalSize: {TotalSize}";

    private sealed class TotalSizeStartIndexSizeEqualityComparer : IEqualityComparer<IVirtualResponse?>
    {
        public bool Equals(IVirtualResponse? x, IVirtualResponse? y)
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

            return x.TotalSize == y.TotalSize && x.StartIndex == y.StartIndex && x.Size == y.Size;
        }

        public int GetHashCode(IVirtualResponse? obj)
        {
            if (obj is null)
            {
                return 0;
            }

            unchecked
            {
                var hashCode = obj.TotalSize;
                hashCode = (hashCode * 397) ^ obj.StartIndex;
                hashCode = (hashCode * 397) ^ obj.Size;
                return hashCode;
            }
        }
    }
}
