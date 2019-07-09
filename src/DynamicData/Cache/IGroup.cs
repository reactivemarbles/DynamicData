// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An update stream which has been grouped by a common key
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of value used to group the original stream</typeparam>
    public interface IGroup<TObject, TKey, out TGroupKey> : IKey<TGroupKey>
    {
        /// <summary>
        /// The observable for the group
        /// </summary>
        /// <value>
        /// The observable.
        /// </value>
        IObservableCache<TObject, TKey> Cache { get; }
    }
}
