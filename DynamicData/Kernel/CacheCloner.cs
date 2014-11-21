using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{


    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal class CacheCloner<TObject, TKey> 
    {
        private readonly ICache<TObject, TKey> _cache;

        public CacheCloner(ICache<TObject, TKey> cache)
        {
            if (cache == null) throw new ArgumentNullException("cache");
            _cache = cache;
        }
        public CacheCloner()
        {
            _cache = new Cache<TObject, TKey>();
        }

        public void Clone(IChangeSet<TObject, TKey> updates)
        {
            if (updates == null) throw new ArgumentNullException("updates");
            foreach (var item in updates)
            {
                switch (item.Reason)
                {
                    case ChangeReason.Update:
                    case ChangeReason.Add:
                        {
                            _cache.AddOrUpdate(item.Current, item.Key);
                        }
                        break;
                    case ChangeReason.Remove:
                        _cache.Remove(item.Key);
                        break;
                }
            }
        }
    }
}