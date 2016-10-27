using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class EditDiff<TObject, TKey>
    {
        private readonly ISourceCache<TObject, TKey> _source;
        private readonly Func<TObject, TObject, bool> _areEqual;
        private readonly IEqualityComparer<KeyValuePair<TKey, TObject>> _keyComparer = new KeyComparer<TObject, TKey>();

        public EditDiff([NotNull] ISourceCache<TObject, TKey> source, [NotNull] Func<TObject, TObject, bool> areEqual)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (areEqual == null) throw new ArgumentNullException(nameof(areEqual));
            _source = source;
            _areEqual = areEqual;
        }

        public void Edit(IEnumerable<TObject> items)
        {
            _source.Edit(innerCache =>
            {
                var originalItems = innerCache.KeyValues.AsArray();
                var newItems = innerCache.GetKeyValues(items).AsArray();

                var removes = originalItems.Except(newItems, _keyComparer).ToArray();
                var adds = newItems.Except(originalItems, _keyComparer).ToArray();

                //calculate intersect where the item has changed.
                var intersect = newItems
                        .Select(kvp => new { Original = innerCache.Lookup(kvp.Key), NewItem = kvp })
                        .Where(x => x.Original.HasValue && !_areEqual(x.Original.Value, x.NewItem.Value))
                        .Select(x => new KeyValuePair<TKey, TObject>(x.NewItem.Key, x.NewItem.Value))
                        .ToArray();

                innerCache.Remove(removes.Select(kvp => kvp.Key));
                innerCache.AddOrUpdate(adds.Union(intersect));
            });
        }
    }
}
