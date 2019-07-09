// ReSharper disable once CheckNamespace
namespace DynamicData.Operators
{
    /// <summary>
    /// Response from the pagation operator
    /// </summary>
    public interface IPageResponse
    {
        /// <summary>
        /// The size of the page.
        /// </summary>
        int PageSize { get; }

        /// <summary>
        /// The current page
        /// </summary>
        int Page { get; }

        /// <summary>
        /// Total number of pages.
        /// </summary>
        int Pages { get; }

        /// <summary>
        /// The total number of records in the underlying cache
        /// </summary>
        int TotalSize { get; }
    }
}
