// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the SizeLimiter class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="size">The size value.</param>
internal sealed class SizeLimiter<TObject, TKey>(int size)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly ChangeAwareCache<ExpirableItem<TObject, TKey>, TKey> _cache = new();

    /// <summary>
    /// Executes the Change operation.
    /// </summary>
    /// <param name="updates">The updates value.</param>
    /// <returns>The result of the operation.</returns>
    public IChangeSet<TObject, TKey> Change(IChangeSet<ExpirableItem<TObject, TKey>, TKey> updates)
    {
        _cache.Clone(updates);

        var itemsToExpire = _cache.KeyValues.OrderByDescending(exp => exp.Value.ExpireAt).Skip(size).Select(exp => new Change<TObject, TKey>(ChangeReason.Remove, exp.Key, exp.Value.Value)).ToList();

        if (itemsToExpire.Count > 0)
        {
            _cache.Remove(itemsToExpire.Select(exp => exp.Key));
        }

        var notifications = _cache.CaptureChanges();
        var changed = notifications.Select(update => new Change<TObject, TKey>(update.Reason, update.Key, update.Current.Value, update.Previous.HasValue ? update.Previous.Value.Value : ReactiveUI.Primitives.Optional<TObject>.None));

        return new ChangeSet<TObject, TKey>(changed);
    }

    /// <summary>
    /// Executes the CloneAndReturnExpiredOnly operation.
    /// </summary>
    /// <param name="updates">The updates value.</param>
    /// <returns>The result of the operation.</returns>
    public KeyValuePair<TKey, TObject>[] CloneAndReturnExpiredOnly(IChangeSet<ExpirableItem<TObject, TKey>, TKey> updates)
    {
        _cache.Clone(updates);
        _cache.CaptureChanges(); // Clear any changes

        return [.. _cache.KeyValues.OrderByDescending(exp => exp.Value.Index).Skip(size).Select(kvp => new KeyValuePair<TKey, TObject>(kvp.Key, kvp.Value.Value))];
    }
}
