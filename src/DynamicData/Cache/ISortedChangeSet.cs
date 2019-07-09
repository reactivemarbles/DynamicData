// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An update collection as per the system convention additionally providing a sorted set of the underling state
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface ISortedChangeSet<TObject, TKey> : IChangeSet<TObject, TKey>
    {
        /// <summary>
        /// All cached items in sort order
        /// </summary>
        IKeyValueCollection<TObject, TKey> SortedItems { get; }
    }
}
