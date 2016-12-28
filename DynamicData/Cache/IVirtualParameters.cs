// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Defines values used to virtualise the result set
    /// </summary>
    public interface IVirtualResponse
    {
        /// <summary>
        /// The requested size of the virtualised data
        /// </summary>
        int Size { get; }

        /// <summary>
        /// The start index.
        /// </summary>
        /// 
        int StartIndex { get; }

        /// <summary>
        /// Gets the total size of the underlying cache
        /// </summary>
        /// <value>
        /// The total size.
        /// </value>
        int TotalSize { get; }
    }
}
