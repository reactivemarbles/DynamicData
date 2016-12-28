// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal interface ICache<TObject, TKey> : IQuery<TObject, TKey>
    {
        void Clone(IChangeSet<TObject, TKey> changes);
        void AddOrUpdate(TObject item, TKey key);
        void Remove(TKey key);
        void Clear();
    }
}
