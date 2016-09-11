using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache
{
    internal class EditDiff<TObject, TKey>
    {
        private readonly ISourceCache<TObject, TKey> _source;
        private readonly Func<TObject, TObject, bool> _hasChanged;
        private readonly IEqualityComparer<KeyValuePair<TKey, TObject>> _keyComparer = new KeyComparer<TObject, TKey>();

        public EditDiff([NotNull] ISourceCache<TObject, TKey> source, [NotNull] Func<TObject, TObject, bool> hasChanged)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (hasChanged == null) throw new ArgumentNullException(nameof(hasChanged));
            _source = source;
            _hasChanged = hasChanged;
        }

        public void Edit(IEnumerable<TObject> items)
        {
            _source.Edit(innerCache =>
            {
                var originalItems = innerCache.KeyValues.AsArray();
                var newItems = innerCache.GetKeyValues(items).AsArray();

                var removes = originalItems.Except(newItems, _keyComparer);
                var adds = newItems.Except(originalItems, _keyComparer);

                innerCache.Remove(removes.Select(kvp => kvp.Key));
                innerCache.AddOrUpdate(adds);

                //calculate intersect where the item has changed.
                var updated = newItems.Select(kvp =>
                {
                    var key = kvp.Key;
                    var original = innerCache.Lookup(key);
                    var hasUpdated = original.ConvertOr(orig => _hasChanged(kvp.Value, orig), () => false);
                    return new { Key = key, NewItem = kvp.Value, OriginalItem = original, HasUpdated= hasUpdated };
                })
                .Where(x=> x.HasUpdated)
                .Select(x=> new KeyValuePair<TKey, TObject>(x.Key,x.NewItem));
               
                innerCache.AddOrUpdate(updated);
            });
        }
    }
}
