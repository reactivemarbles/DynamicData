// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
///     Combines multiple caches using logical operators.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="type">The type value.</param>
/// <param name="updatedCallback">The updatedCallback value.</param>
internal sealed class Combiner<TObject, TKey>(CombineOperator type, Action<IChangeSet<TObject, TKey>> updatedCallback)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _combinedCache field.
    /// </summary>
    private readonly ChangeAwareCache<TObject, TKey> _combinedCache = new();

    /// <summary>
    /// The _locker field.
    /// </summary>
    private readonly Lock _locker = new();

    /// <summary>
    /// The _sourceCaches field.
    /// </summary>
    private readonly IList<Cache<TObject, TKey>> _sourceCaches = [];

    /// <summary>
    /// Executes the Subscribe operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <returns>The result of the operation.</returns>
    public IDisposable Subscribe(IObservable<IChangeSet<TObject, TKey>>[] source)
    {
        // subscribe
        var disposable = new CompositeDisposable();
        lock (_locker)
        {
            var caches = Enumerable.Range(0, source.Length).Select(_ => new Cache<TObject, TKey>());
            _sourceCaches.AddRange(caches);

            foreach (var pair in source.Zip(_sourceCaches, (item, cache) => new { Item = item, Cache = cache }))
            {
                var subscription = pair.Item.Subscribe(updates => Update(pair.Cache, updates));
                disposable.Add(subscription);
            }
        }

        return disposable;
    }

    /// <summary>
    /// Executes the MatchesConstraint operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    private bool MatchesConstraint(TKey key)
    {
        switch (type)
        {
            case CombineOperator.And:
                {
                    return _sourceCaches.All(s => s.Lookup(key).HasValue);
                }

            case CombineOperator.Or:
                {
                    return _sourceCaches.Any(s => s.Lookup(key).HasValue);
                }

            case CombineOperator.Xor:
                {
                    return _sourceCaches.Count(s => s.Lookup(key).HasValue) == 1;
                }

            case CombineOperator.Except:
                {
                    var first = _sourceCaches.Take(1).Any(s => s.Lookup(key).HasValue);
                    var others = _sourceCaches.Skip(1).Any(s => s.Lookup(key).HasValue);
                    return first && !others;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(key));
        }
    }

    /// <summary>
    /// Executes the Update operation.
    /// </summary>
    /// <param name="cache">The cache value.</param>
    /// <param name="updates">The updates value.</param>
    private void Update(Cache<TObject, TKey> cache, IChangeSet<TObject, TKey> updates)
    {
        ChangeSet<TObject, TKey> notifications;

        lock (_locker)
        {
            // update cache for the individual source
            cache.Clone(updates);

            // update combined
            notifications = UpdateCombined(updates);
        }

        if (notifications.Count != 0)
        {
            updatedCallback(notifications);
        }
    }

    /// <summary>
    /// Executes the UpdateCombined operation.
    /// </summary>
    /// <param name="updates">The updates value.</param>
    /// <returns>The result of the operation.</returns>
    private ChangeSet<TObject, TKey> UpdateCombined(IChangeSet<TObject, TKey> updates)
    {
        // child caches have been updated before we reached this point.
        foreach (var update in updates.ToConcreteType())
        {
            var key = update.Key;
            switch (update.Reason)
            {
                case ChangeReason.Add:
                case ChangeReason.Update:
                    {
                        // get the current key.
                        // check whether the item should belong to the cache
                        var cached = _combinedCache.Lookup(key);
                        var contained = cached.HasValue;
                        var match = MatchesConstraint(key);

                        if (match)
                        {
                            if (contained)
                            {
                                if (!ReferenceEquals(update.Current, cached.Value))
                                {
                                    _combinedCache.AddOrUpdate(update.Current, key);
                                }
                            }
                            else
                            {
                                _combinedCache.AddOrUpdate(update.Current, key);
                            }
                        }
                        else if (contained)
                        {
                            _combinedCache.Remove(key);
                        }
                    }

                    break;

                case ChangeReason.Remove:
                    {
                        var cached = _combinedCache.Lookup(key);
                        var contained = cached.HasValue;
                        var shouldBeIncluded = MatchesConstraint(key);

                        if (shouldBeIncluded)
                        {
                            var firstOne = _sourceCaches.Select(s => s.Lookup(key)).SelectValues().First();

                            if (!cached.HasValue)
                            {
                                _combinedCache.AddOrUpdate(firstOne, key);
                            }
                            else if (!ReferenceEquals(firstOne, cached.Value))
                            {
                                _combinedCache.AddOrUpdate(firstOne, key);
                            }
                        }
                        else if (contained)
                        {
                            _combinedCache.Remove(key);
                        }
                    }

                    break;

                case ChangeReason.Refresh:
                    {
                        _combinedCache.Refresh(key);
                    }

                    break;
            }
        }

        return _combinedCache.CaptureChanges();
    }
}
