
namespace DynamicData.Binding
{
    /// <summary>
    /// Represents an adaptor which is used to update observable collection from
    /// a sorted change set stream
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface ISortedObservableCollectionAdaptor<TObject, TKey>
    {
        /// <summary>
        /// Maintains the specified collection from the changes
        /// </summary>
        /// <param name="changes">The changes.</param>
        /// <param name="collection">The collection.</param>
        void Adapt(ISortedChangeSet<TObject, TKey> changes, IObservableCollection<TObject> collection);
    }
}
