using System;
using System.Collections.Generic;

namespace DynamicData
{
    /// <summary>
    /// Defines values used to virtualise the result set
    /// </summary>
    internal class VirtualResponse : IEquatable<IVirtualResponse>, IVirtualResponse
    {
        private readonly int _size;
        private readonly int _startIndex;
        private readonly int _totalSize;

        public VirtualResponse(int size, int startIndex, int totalSize)
        {
            _size = size;
            _startIndex = startIndex;
            _totalSize = totalSize;
        }

        /// <summary>
        /// The requested size of the virtualised data
        /// </summary>
        public int Size { get { return _size; } }

        /// <summary>
        /// The starting index
        /// </summary>
        public int StartIndex { get { return _startIndex; } }

        /// <summary>
        /// Gets the total size of the underlying cache
        /// </summary>
        public int TotalSize { get { return _totalSize; } }

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
            return TotalSizeStartIndexSizeComparerInstance.Equals(this, other);
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
                int hashCode = _size;
                hashCode = (hashCode * 397) ^ _startIndex;
                hashCode = (hashCode * 397) ^ _totalSize;
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

        private static readonly IEqualityComparer<IVirtualResponse> TotalSizeStartIndexSizeComparerInstance = new TotalSizeStartIndexSizeEqualityComparer();

        public static IEqualityComparer<IVirtualResponse> DefaultComparer { get { return TotalSizeStartIndexSizeComparerInstance; } }

        #endregion

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return string.Format("Size: {0}, StartIndex: {1}, TotalSize: {2}", _size, _startIndex, _totalSize);
        }
    }
}
