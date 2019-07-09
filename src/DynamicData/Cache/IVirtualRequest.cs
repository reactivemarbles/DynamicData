// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A request to virtualise a stream
    /// </summary>
    public interface IVirtualRequest
    {
        /// <summary>
        /// The number of records to return
        /// </summary>
        int Size { get; }

        /// <summary>
        /// The start index
        /// </summary>
        int StartIndex { get; }
    }
}
