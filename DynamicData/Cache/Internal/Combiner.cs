using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    /// <summary>
    ///     Combines multiple caches using logical opertators
    /// </summary>
    internal sealed class Combiner<TObject, TKey>
    {
        private readonly IList<Cache<TObject, TKey>> _sourceCaches = new List<Cache<TObject, TKey>>();
        private readonly Cache<TObject, TKey> _combinedCache = new Cache<TObject, TKey>();

        private readonly object _locker = new object();
        private readonly CombineOperator _type;
        private readonly Action<IChangeSet<TObject, TKey>> _updatedCallback;

        public Combiner(CombineOperator type, Action<IChangeSet<TObject, TKey>> updatedCallback)
        {
            _type = type;
            _updatedCallback = updatedCallback;
        }

        public IDisposable Subscribe(IObservable<IChangeSet<TObject, TKey>>[] source)
        {
            //subscribe
            var disposable = new CompositeDisposable();
            lock (_locker)
            {
                foreach (var item in source)
                {
                    var cache = new Cache<TObject, TKey>();
                    _sourceCaches.Add(cache);

                    var subsription = item.Subscribe(updates => Update(cache, updates));
                    disposable.Add(subsription);
                }
            }
            return disposable;
        }

        private void Update(Cache<TObject, TKey> cache, IChangeSet<TObject, TKey> updates)
        {
            IChangeSet<TObject, TKey> notifications;

            lock (_locker)
            {
                //update cache for the individual source
                cache.Clone(updates);

                //update combined 
                notifications = UpdateCombined(updates);
            }

            if (notifications.Count != 0)
                _updatedCallback(notifications);
        }

        private IChangeSet<TObject, TKey> UpdateCombined(IChangeSet<TObject, TKey> updates)
        {
            //child caches have been updated before we reached this point.
            var updater = new IntermediateUpdater<TObject, TKey>(_combinedCache);

            foreach (var update in updates)
            {
                TKey key = update.Key;
                switch (update.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                    {
                        // get the current key.
                        //check whether the item should belong to the cache
                        var cached = updater.Lookup(key);
                        var contained = cached.HasValue;
                        var match = MatchesConstraint(key);

                        if (match)
                        {
                            if (contained)
                            {
                                if (!ReferenceEquals(update.Current, cached.Value))
                                    updater.AddOrUpdate(update.Current, key);
                            }
                            else
                            {
                                updater.AddOrUpdate(update.Current, key);
                            }
                        }
                        else
                        {
                            if (contained)
                                updater.Remove(key);
                        }
                    }
                        break;

                    case ChangeReason.Remove:
                    {
                        var cached = updater.Lookup(key);
                        var contained = cached.HasValue;
                        bool shouldBeIncluded = MatchesConstraint(key);

                        if (shouldBeIncluded)
                        {
                            var firstOne = _sourceCaches.Select(s => s.Lookup(key))
                                                        .SelectValues()
                                                        .First();

                            if (!cached.HasValue)
                            {
                                updater.AddOrUpdate(firstOne, key);
                            }
                            else if (!ReferenceEquals(firstOne, cached.Value))
                            {
                                updater.AddOrUpdate(firstOne, key);
                            }
                        }
                        else
                        {
                            if (contained)
                                updater.Remove(key);
                        }
                    }
                        break;

                    case ChangeReason.Evaluate:
                    {
                        updater.Evaluate(key);
                    }
                        break;
                }
            }
            return updater.AsChangeSet();
        }

        private bool MatchesConstraint(TKey key)
        {
            switch (_type)
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
                    bool first = _sourceCaches.Take(1).Any(s => s.Lookup(key).HasValue);
                    bool others = _sourceCaches.Skip(1).Any(s => s.Lookup(key).HasValue);
                    return first && !others;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
