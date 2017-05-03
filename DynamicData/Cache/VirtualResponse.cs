using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Defines values used to virtualise the result set
    /// </summary>
    internal sealed class VirtualResponse : IEquatable<IVirtualResponse>, IVirtualResponse
    {
        public VirtualResponse(int size, int startIndex, int totalSize)
        {
            Size = size;
            StartIndex = startIndex;
            TotalSize = totalSize;
        }

        /// <summary>
        /// The requested size of the virtualised data
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// The starting index
        /// </summary>
        public int StartIndex { get; }

        /// <summary>
        /// Gets the total size of the underlying cache
        /// </summary>
        public int TotalSize { get; }

        #region Equality members

        /// <summary>
        ///     Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        ///     true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(IVirtualResponse other)
        {
            return STotalSizeStartIndexSizeComparerInstance.Equals(this, other);
        }

        /// <summary>
        ///     Determines whether the specified <see cref="T:System.Object" /> is equal to the current <see cref="T:System.Object" />.
        /// </summary>
        /// <returns>
        ///     true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return Equals((IVirtualResponse)obj);
        }

        /// <summary>
        ///     Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        ///     A hash code for the current <see cref="T:System.Object" />.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Size;
                hashCode = (hashCode * 397) ^ StartIndex;
                hashCode = (hashCode * 397) ^ TotalSize;
                return hashCode;
            }
        }

        #endregion

        #region TotalSizeStartIndexSizeEqualityComparer

        private sealed class TotalSizeStartIndexSizeEqualityComparer : IEqualityComparer<IVirtualResponse>
        {
            public bool Equals(IVirtualResponse x, IVirtualResponse y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.TotalSize == y.TotalSize && x.StartIndex == y.StartIndex && x.Size == y.Size;
            }

            public int GetHashCode(IVirtualResponse obj)
            {
                unchecked
                {
                    int hashCode = obj.TotalSize;
                    hashCode = (hashCode * 397) ^ obj.StartIndex;
                    hashCode = (hashCode * 397) ^ obj.Size;
                    return hashCode;
                }
            }
        }

        private static readonly IEqualityComparer<IVirtualResponse> STotalSizeStartIndexSizeComparerInstance = new TotalSizeStartIndexSizeEqualityComparer();

        public static IEqualityComparer<IVirtualResponse> DefaultComparer => STotalSizeStartIndexSizeComparerInstance;

        #endregion

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return $"Size: {Size}, StartIndex: {StartIndex}, TotalSize: {TotalSize}";
        }
    }
}
