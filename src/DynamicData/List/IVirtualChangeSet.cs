// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Represents a subset of data reduced by a defined set of parameters
    /// Changes are always published in the order.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    public interface IVirtualChangeSet<T> : IChangeSet<T>
    {
        /// <summary>
        /// The paramaters used to virtualise the stream
        /// </summary>
        IVirtualResponse Response { get; }
    }
}