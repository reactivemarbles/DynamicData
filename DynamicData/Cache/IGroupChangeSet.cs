// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    ///  A grouped update collection
    /// </summary>
    /// <typeparam name="TObject">The source object type</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>s
    /// <typeparam name="TGroupKey">The value on which the stream has been grouped</typeparam>
    public interface IGroupChangeSet<TObject, TKey, TGroupKey> :
        IChangeSet<IGroup<TObject, TKey, TGroupKey>, TGroupKey>
    {
    }
}
