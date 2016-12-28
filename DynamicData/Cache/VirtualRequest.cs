using System;
using System.Collections.Generic;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A request object for virtualisation
    /// </summary>
    public class VirtualRequest : IEquatable<IVirtualRequest>, IVirtualRequest
    {
        /// <summary>
        /// The default request value
        /// </summary>
        public static readonly VirtualRequest Default = new VirtualRequest(0, 25);

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualRequest"/> class.
        /// </summary>
        /// <param name="startIndex">The start index.</param>
        /// <param name="size">The size.</param>
        public VirtualRequest(int startIndex, int size)
        {
            Size = size;
            StartIndex = startIndex;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualRequest"/> class.
        /// </summary>
        public VirtualRequest()
        {
        }

        /// <summary>
        /// The maximumn number of items to return
        /// </summary>
        public int Size { get; } = 25;

        /// <summary>
        /// The first index in the virualised list
        /// </summary>
        public int StartIndex { get; }

        #region Equality members

        private sealed class StartIndexSizeEqualityComparer : IEqualityComparer<IVirtualRequest>
        {
            public bool Equals(IVirtualRequest x, IVirtualRequest y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.StartIndex == y.StartIndex && x.Size == y.Size;
            }

            public int GetHashCode(IVirtualRequest obj)
            {
                unchecked
                {
                    return (obj.StartIndex * 397) ^ obj.Size;
                }
            }
        }

        private static readonly IEqualityComparer<IVirtualRequest> s_startIndexSizeComparerInstance = new StartIndexSizeEqualityComparer();

        /// <summary>
        /// Gets the start index size comparer.
        /// </summary>
        /// <value>
        /// The start index size comparer.
        /// </value>
        public static IEqualityComparer<IVirtualRequest> StartIndexSizeComparer { get { return s_startIndexSizeComparerInstance; } }

        /// <summary>
        ///     Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        ///     true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(IVirtualRequest other)
        {
            return s_startIndexSizeComparerInstance.Equals(this, other);
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
            return Equals((IVirtualRequest)obj);
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
                return (StartIndex * 397) ^ Size;
            }
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("StartIndex: {0}, Size: {1}", StartIndex, Size);
        }
    }
}
