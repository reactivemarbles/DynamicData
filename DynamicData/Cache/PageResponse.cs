using System;
using System.Collections.Generic;
using DynamicData.Operators;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal sealed class PageResponse : IEquatable<IPageResponse>, IPageResponse
    {
        public PageResponse(int pageSize, int totalSize, int page, int pages)
        {
            PageSize = pageSize;
            TotalSize = totalSize;
            Page = page;
            Pages = pages;
        }

        public int PageSize { get; }

        public int Page { get; }

        public int Pages { get; }

        public int TotalSize { get; }

        #region Equality members

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(IPageResponse other)
        {
            return DefaultComparer.Equals(this, other);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. </param>
        public override bool Equals(object obj)
        {
            if (!(obj is IPageResponse)) return false;
            return Equals(obj as IPageResponse);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
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

        #endregion

        #region PageResponseEqualityComparer

        private sealed class PageResponseEqualityComparer : IEqualityComparer<IPageResponse>
        {
            public bool Equals(IPageResponse x, IPageResponse y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.PageSize == y.PageSize && x.TotalSize == y.TotalSize && x.Page == y.Page && x.Pages == y.Pages;
            }

            public int GetHashCode(IPageResponse obj)
            {
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

        private static readonly IEqualityComparer<IPageResponse> s_pageResponseComparerInstance = new PageResponseEqualityComparer();

        public static IEqualityComparer<IPageResponse> DefaultComparer { get { return s_pageResponseComparerInstance; } }

        #endregion

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return $"Page: {Page}, PageSize: {PageSize}, Pages: {Pages}, TotalSize: {TotalSize}";
        }
    }
}
