// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Represents a subset of data reduced by a defined set of parameters
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IVirtualChangeSet<TObject, TKey> : ISortedChangeSet<TObject, TKey>
    {
        /// <summary>
        /// The paramaters used to virtualise the stream
        /// </summary>
        IVirtualResponse Response { get; }
    }
}
