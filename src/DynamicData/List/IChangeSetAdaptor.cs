
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A simple adaptor to inject side effects into a changeset observable
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    public interface IChangeSetAdaptor<T>
    {
        /// <summary>
        /// Adapts the specified change.
        /// </summary>
        /// <param name="change">The change.</param>
        void Adapt(IChangeSet<T> change);
    }
}
