// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the EditDiff class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="areEqual">The areEqual value.</param>
internal sealed class EditDiff<TObject, TKey>(ISourceCache<TObject, TKey> source, Func<TObject, TObject, bool> areEqual)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _areEqual field.
    /// </summary>
    private readonly Func<TObject, TObject, bool> _areEqual = areEqual ?? throw new ArgumentNullException(nameof(areEqual));

    /// <summary>
    /// The _keyComparer field.
    /// </summary>
    private readonly IEqualityComparer<KeyValuePair<TKey, TObject>> _keyComparer = new KeyComparer<TObject, TKey>();

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly ISourceCache<TObject, TKey> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Edit operation.
    /// </summary>
    /// <param name="items">The items value.</param>
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
