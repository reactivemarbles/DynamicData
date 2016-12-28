// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A gouping of observable lists
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TGroup">The type of the group.</typeparam>
    public interface IGroup<TObject, out TGroup>
    {
        /// <summary>
        /// Gets the group key.
        /// </summary>
        TGroup GroupKey { get; }

        /// <summary>
        /// Gets the observable list.
        /// </summary>
        IObservableList<TObject> List { get; }
    }
}
