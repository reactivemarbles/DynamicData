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

        /// <summary>
        /// Gets the start index size comparer.
        /// </summary>
        /// <value>
        /// The start index size comparer.
        /// </value>
        public static IEqualityComparer<IVirtualRequest> StartIndexSizeComparer { get; } = new StartIndexSizeEqualityComparer();

        /// <inheritdoc />
        public bool Equals(IVirtualRequest other)
        {
            return StartIndexSizeComparer.Equals(this, other);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals((IVirtualRequest)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (StartIndex * 397) ^ Size;
            }
        }

        #endregion

        /// <inheritdoc />
        public override string ToString() => $"StartIndex: {StartIndex}, Size: {Size}";
    }
}
