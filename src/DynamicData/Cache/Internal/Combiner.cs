// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

/// <summary>
///     Combines multiple caches using logical operators.
/// </summary>
internal sealed class Combiner<TObject, TKey>(CombineOperator type, Action<IChangeSet<TObject, TKey>> updatedCallback)
    where TObject : notnull
    where TKey : notnull
{
    private readonly ChangeAwareCache<TObject, TKey> _combinedCache = new();

#if NET9_0_OR_GREATER
    private readonly Lock _locker = new();
#else
    private readonly object _locker = new();
#endif

    private readonly IList<Cache<TObject, TKey>> _sourceCaches = [];

    public IDisposable Subscribe(IObservable<IChangeSet<TObject, TKey>>[] source, Action<Exception> onError, Action onCompleted)
    {
        // Merging semantics: the result finishes only once every source has.
        var pending = source.Length;
        if (pending == 0)
        {
            onCompleted();
            return Disposable.Empty;
        }

        // Each source updates shared state under _locker, but delivery has to be serialized too:
        // without this, two sources can compute their notifications, leave the lock, and then both
        // be inside updatedCallback at the same time. The queue takes the notification while the
        // lock is held and drains it after the lock is released, so deliveries stay ordered and
        // one at a time without a subscriber being able to block a producer.
        var queue = new DeliveryQueue<IChangeSet<TObject, TKey>>(
            _locker,
            Observer.Create(updatedCallback, onError, onCompleted));

        // subscribe
        var disposable = new CompositeDisposable();
        lock (_locker)
        {
            var caches = Enumerable.Range(0, source.Length).Select(_ => new Cache<TObject, TKey>());
            _sourceCaches.AddRange(caches);

            foreach (var pair in source.Zip(_sourceCaches, (item, cache) => new { Item = item, Cache = cache }))
            {
                var subscription = pair.Item.Subscribe(
                    updates => Update(queue, pair.Cache, updates),
                    queue.OnError,
                    () =>
                    {
                        if (Interlocked.Decrement(ref pending) == 0)
                        {
                            queue.OnCompleted();
                        }
                    });

                disposable.Add(subscription);
            }
        }

        // Queue last: the subscriptions are torn down first, so any terminal notification still in
        // flight is delivered through a queue that is still running.
        disposable.Add(queue);

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

    private void Update(DeliveryQueue<IChangeSet<TObject, TKey>> queue, Cache<TObject, TKey> cache, IChangeSet<TObject, TKey> updates)
    {
        using var scope = queue.AcquireLock();

        // update cache for the individual source
        cache.Clone(updates);

        // update combined
        var notifications = UpdateCombined(updates);

        if (notifications.Count != 0)
        {
            scope.EnqueueNext(notifications);
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
