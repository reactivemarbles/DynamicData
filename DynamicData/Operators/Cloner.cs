using DynamicData.Kernel;

namespace DynamicData.Operators
{
    /// <summary>
    /// Maintains a cache from a sequence of change sets 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal sealed class Cloner<TObject, TKey> 
    {
        private readonly ICache<TObject, TKey> _cache = new Cache<TObject, TKey>();
        private readonly CacheCloner<TObject, TKey> _updater;

        public Cloner()
        {
            _updater = new CacheCloner<TObject, TKey>(_cache);
        }

        public Cloner(ICache<TObject, TKey> cache)
        {
            _cache = cache;
            _updater = new CacheCloner<TObject, TKey>(_cache);
        }

        #region IFilterer<T> Members

        public IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates)
        {
           _updater.Clone(updates);

            return updates;
        }

        #endregion

    }
}