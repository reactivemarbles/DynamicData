

using System.Collections.Generic;

namespace DynamicData.Kernel
{
    internal interface ICache<TObject, TKey> : IQuery<TObject, TKey>
    {
        new IEnumerable<KeyValuePair<TKey,TObject>> KeyValues { get; }

        void Load(IEnumerable<KeyValuePair<TKey,TObject>> items);

        void AddOrUpdate(IEnumerable<KeyValuePair<TKey,TObject>> items);
        void AddOrUpdate(KeyValuePair<TKey,TObject> item);
        void AddOrUpdate(TObject item, TKey key);

        void Remove(IEnumerable<KeyValuePair<TKey,TObject>> items);
        void Remove(IEnumerable<TKey> items);
        void Remove(KeyValuePair<TKey,TObject> item);
        void Remove(TKey key);
        void Clear();
    }
}