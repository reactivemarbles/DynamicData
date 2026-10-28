// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
///     Combines multiple caches using logical operators.
///     Every source is routed through one <see cref="SharedDeliveryQueue"/>, so updates from different sources are
///     applied and delivered one at a time, and no lock is held while <paramref name="updatedCallback"/> runs.
/// </summary>
internal sealed class Combiner<TObject, TKey>(CombineOperator type, Action<IChangeSet<TObject, TKey>> updatedCallback)
    where TObject : notnull
    where TKey : notnull
{
    private readonly ChangeAwareCache<TObject, TKey> _combinedCache = new();

    private readonly IList<Cache<TObject, TKey>> _sourceCaches = [];

    public IDisposable Subscribe(IObservable<IChangeSet<TObject, TKey>>[] source)
    {
        var queue = new SharedDeliveryQueue();
        var disposable = new CompositeDisposable(queue);

        _sourceCaches.AddRange(Enumerable.Range(0, source.Length).Select(_ => new Cache<TObject, TKey>()));

        foreach (var pair in source.Zip(_sourceCaches, (item, cache) => new { Item = item, Cache = cache }))
        {
            disposable.Add(pair.Item.SynchronizeSafe(queue).Subscribe(updates => Update(pair.Cache, updates)));
        }

        return disposable;
    }

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

    private void Update(Cache<TObject, TKey> cache, IChangeSet<TObject, TKey> updates)
    {
        // update cache for the individual source
        cache.Clone(updates);

        // update combined
        var notifications = UpdateCombined(updates);

        if (notifications.Count != 0)
        {
            updatedCallback(notifications);
        }
    }

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
