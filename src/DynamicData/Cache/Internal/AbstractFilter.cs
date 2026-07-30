// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the AbstractFilter class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal abstract class AbstractFilter<TObject, TKey> : IFilter<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _cache field.
    /// </summary>
    private readonly ChangeAwareCache<TObject, TKey> _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractFilter{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="filter">The filter value.</param>
    protected AbstractFilter(ChangeAwareCache<TObject, TKey> cache, Func<TObject, bool>? filter)
    {
        ArgumentExceptionHelper.ThrowIfNull(cache);

        _cache = cache;
        Filter = filter ?? (_ => true);
    }

    /// <summary>
    /// Gets the Filter value.
    /// </summary>
    public Func<TObject, bool> Filter { get; }

    /// <summary>
    /// Executes the Refresh operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <returns>The result of the operation.</returns>
    public IChangeSet<TObject, TKey> Refresh(IEnumerable<KeyValuePair<TKey, TObject>> items)
    {
        // this is an internal method only so we can be sure there are no duplicate keys in the result
        // (therefore safe to parallelise)
        ReactiveUI.Primitives.Optional<Change<TObject, TKey>> Factory(KeyValuePair<TKey, TObject> kv)
        {
            var existing = _cache.Lookup(kv.Key);
            var matches = Filter(kv.Value);

            if (matches)
            {
                if (!existing.HasValue)
                {
                    return new Change<TObject, TKey>(ChangeReason.Add, kv.Key, kv.Value);
                }
            }
            else if (existing.HasValue)
            {
                return new Change<TObject, TKey>(ChangeReason.Remove, kv.Key, kv.Value, existing);
            }

            return ReactiveUI.Primitives.Optional<Change<TObject, TKey>>.None;
        }

        var result = Refresh(items, Factory);
        _cache.Clone(new ChangeSet<TObject, TKey>(result));

        return _cache.CaptureChanges();
    }

    /// <summary>
    /// Executes the Update operation.
    /// </summary>
    /// <param name="updates">The updates value.</param>
    /// <returns>The result of the operation.</returns>
    public IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates)
    {
        var withFilter = GetChangesWithFilter(updates.ToConcreteType());
        return ProcessResult(withFilter);
    }

    /// <summary>
    /// Executes the GetChangesWithFilter operation.
    /// </summary>
    /// <param name="updates">The updates value.</param>
    /// <returns>The result of the operation.</returns>
    protected abstract IEnumerable<UpdateWithFilter> GetChangesWithFilter(ChangeSet<TObject, TKey> updates);

    /// <summary>
    /// Executes the Refresh operation.
    /// </summary>
    /// <param name="items">The items value.</param>
    /// <param name="factory">The factory value.</param>
    /// <returns>The result of the operation.</returns>
    protected abstract IEnumerable<Change<TObject, TKey>> Refresh(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, ReactiveUI.Primitives.Optional<Change<TObject, TKey>>> factory);

    /// <summary>
    /// Executes the ProcessResult operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <returns>The result of the operation.</returns>
    private ChangeSet<TObject, TKey> ProcessResult(IEnumerable<UpdateWithFilter> source)
    {
        // Have to process one item at a time as an item can be included multiple
        // times in any batch
        foreach (var item in source)
        {
            var matches = item.IsMatch;
            var key = item.Change.Key;
            var u = item.Change;

            switch (item.Change.Reason)
            {
                case ChangeReason.Add:
                    {
                        if (matches)
                        {
                            _cache.AddOrUpdate(u.Current, u.Key);
                        }
                    }

                    break;

                case ChangeReason.Update:
                    {
                        if (matches)
                        {
                            _cache.AddOrUpdate(u.Current, u.Key);
                        }
                        else
                        {
                            _cache.Remove(u.Key);
                        }
                    }

                    break;

                case ChangeReason.Remove:
                    _cache.Remove(u.Key);
                    break;

                case ChangeReason.Refresh:
                    {
                        var existing = _cache.Lookup(key);
                        if (matches)
                        {
                            if (!existing.HasValue)
                            {
                                _cache.AddOrUpdate(u.Current, u.Key);
                            }
                            else
                            {
                                _cache.Refresh();
                            }
                        }
                        else if (existing.HasValue)
                        {
                            _cache.Remove(u.Key);
                        }
                    }

                    break;
            }
        }

        return _cache.CaptureChanges();
    }

/// <summary>
/// Initializes a new instance of the <see cref="UpdateWithFilter"/> struct.
/// Initializes a new instance of the <see cref="object"/> class.
/// </summary>
/// <param name="isMatch">If the filter is a match.</param>
/// <param name="change">The change.</param>
protected readonly struct UpdateWithFilter(bool isMatch, Change<TObject, TKey> change)
    {
        /// <summary>
        /// Gets the Change value.
        /// </summary>
        public Change<TObject, TKey> Change { get; } = change;

        /// <summary>
        /// Gets the IsMatch value.
        /// </summary>
        public bool IsMatch { get; } = isMatch;
    }
}
