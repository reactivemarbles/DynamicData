using System;
using System.Collections.Generic;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Represents a new page request
    /// </summary>
    public sealed class PageRequest : IPageRequest, IEquatable<IPageRequest>
    {
        /// <summary>
        /// The default page request
        /// </summary>
        public static readonly IPageRequest Default = new PageRequest();

        /// <summary>
        /// Represents an empty page
        /// </summary>
        public static readonly IPageRequest Empty = new PageRequest(0, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="PageRequest"/> class.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="size">The size.</param>
        public PageRequest(int page, int size)
        {
            if (page < 0) throw new ArgumentException("Page must be positive");
            if (size < 0) throw new ArgumentException("Size must be positive");
            Page = page;
            Size = size;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public PageRequest()
        {
        }

        /// <summary>
        /// The page to move to
        /// </summary>
        public int Page { get; } = 1;

        /// <summary>
        /// The page size
        /// </summary>
        public int Size { get; } = 25;

        #region Equality members

        /// <inheritdoc />
        public bool Equals(IPageRequest other)
        {
            return DefaultComparer.Equals(this, other);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (!(obj is IPageRequest))
                return false;
            return Equals((IPageRequest)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Size * 397) ^ Page;
            }
        }

        #endregion

        #region PageSizeEqualityComparer

        private sealed class PageSizeEqualityComparer : IEqualityComparer<IPageRequest>
        {
            public bool Equals(IPageRequest x, IPageRequest y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.Page == y.Page && x.Size == y.Size;
            }

            public int GetHashCode(IPageRequest obj)
            {
                unchecked
                {
                    return (obj.Page * 397) ^ obj.Size;
                }
            }
        }

        private static readonly IEqualityComparer<IPageRequest> _pageSizeComparerInstance = new PageSizeEqualityComparer();

        /// <summary>
        /// Gets the default comparer.
        /// </summary>
        /// <value>
        /// The default comparer.
        /// </value>
        public IEqualityComparer<IPageRequest> DefaultComparer => _pageSizeComparerInstance;

        #endregion

        /// <inheritdoc />
        public override string ToString() => $"Page: {Page}, Size: {Size}";
    }
}
