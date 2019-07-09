// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A collection of distinct value updates.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDistinctChangeSet<T> : IChangeSet<T, T>
    {
    }
}
