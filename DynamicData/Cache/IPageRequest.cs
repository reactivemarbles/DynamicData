// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Represents a new page request
    /// </summary>
    public interface IPageRequest
    {
        /// <summary>
        /// The page to move to
        /// </summary>
        int Page { get; }

        /// <summary>
        /// The page size
        /// </summary>
        int Size { get; }
    }
}
