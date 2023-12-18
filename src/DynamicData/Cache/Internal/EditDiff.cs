// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class EditDiff<TObject, TKey>(ISourceCache<TObject, TKey> source, Func<TObject, TObject, bool> areEqual)
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TObject, bool> _areEqual = areEqual ?? throw new ArgumentNullException(nameof(areEqual));

    private readonly IEqualityComparer<KeyValuePair<TKey, TObject>> _keyComparer = new KeyComparer<TObject, TKey>();

    private readonly ISourceCache<TObject, TKey> _source = source ?? throw new ArgumentNullException(nameof(source));

    public void Edit(IEnumerable<TObject> items) => _source.Edit(
            innerCache =>
            {
                var originalItems = innerCache.KeyValues.AsArray();
                var newItems = innerCache.GetKeyValues(items).AsArray();

                var removes = originalItems.Except(newItems, _keyComparer).ToArray();
                var adds = newItems.Except(originalItems, _keyComparer).ToArray();

                // calculate intersect where the item has changed.
                var intersect = newItems.Select(kvp => new { Original = innerCache.Lookup(kvp.Key), NewItem = kvp }).Where(x => x.Original.HasValue && !_areEqual(x.Original.Value, x.NewItem.Value)).Select(x => new KeyValuePair<TKey, TObject>(x.NewItem.Key, x.NewItem.Value)).ToArray();

                innerCache.Remove(removes.Select(kvp => kvp.Key));
                innerCache.AddOrUpdate(adds.Union(intersect));
            });
}
