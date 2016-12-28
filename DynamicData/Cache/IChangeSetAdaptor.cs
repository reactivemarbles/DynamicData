// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A simple adaptor to inject side effects into a changeset observable
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IChangeSetAdaptor<TObject, TKey>
    {
        /// <summary>
        /// Adapts the specified change.
        /// </summary>
        /// <param name="change">The change.</param>
        void Adapt(IChangeSet<TObject, TKey> change);
    }
}
