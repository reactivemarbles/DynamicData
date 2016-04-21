using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal abstract class AbstractFilter<TObject, TKey> : IFilter<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;
        private readonly Func<TObject, bool> _filter;

        protected AbstractFilter(ICache<TObject, TKey> cache, Func<TObject, bool> filter)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _cache = cache;

            if (filter == null)
            {
                _filter = t => true;
            }
            else
            {
                _filter = filter;
            }
        }

        public Func<TObject, bool> Filter => _filter;

        public IChangeSet<TObject, TKey> Evaluate(IEnumerable<KeyValuePair<TKey, TObject>> items)
        {
            //this is an internal method only so we can be sure there are no duplicate keys in the result
            //(therefore safe to parallelise)
            Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory = kv =>
            {
                var exisiting = _cache.Lookup(kv.Key);
                var matches = _filter(kv.Value);

                if (matches)
                {
                    if (!exisiting.HasValue)
                        return new Change<TObject, TKey>(ChangeReason.Add, kv.Key, kv.Value);
                }
                else
                {
                    if (exisiting.HasValue)
                        return new Change<TObject, TKey>(ChangeReason.Remove, kv.Key, kv.Value, exisiting);
                }

                return Optional.None<Change<TObject, TKey>>();
            };

            var result = Evaluate(items, factory);
            var changes = new ChangeSet<TObject, TKey>(result);
            _cache.Clone(changes);
            return changes;
        }

        protected abstract IEnumerable<Change<TObject, TKey>> Evaluate(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory);

        public IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates)
        {
            var withfilter = GetChangesWithFilter(updates);
            return ProcessResult(withfilter);
        }

        protected abstract IEnumerable<UpdateWithFilter> GetChangesWithFilter(IChangeSet<TObject, TKey> updates);

        private IChangeSet<TObject, TKey> ProcessResult(IEnumerable<UpdateWithFilter> result)
        {
            //alas, have to process one item at a time as an item can be included multiple
            //times in any batch
            var updates = new List<Change<TObject, TKey>>();
            foreach (var item in result)
            {
                var matches = item.IsMatch;
                var key = item.Change.Key;
                var exisiting = _cache.Lookup(key);
                var u = item.Change;

                Optional<Change<TObject, TKey>> change = Optional.None<Change<TObject, TKey>>();
                switch (item.Change.Reason)
                {
                    case ChangeReason.Add:
                    {
                        if (matches)
                        {
                            _cache.AddOrUpdate(u.Current, u.Key);
                            change = new Change<TObject, TKey>(ChangeReason.Add, key, u.Current);
                        }
                    }
                        break;
                    case ChangeReason.Update:
                    {
                        if (matches)
                        {
                            _cache.AddOrUpdate(u.Current, u.Key);
                            change = exisiting.HasValue
                                ? new Change<TObject, TKey>(ChangeReason.Update, key, u.Current, exisiting)
                                : new Change<TObject, TKey>(ChangeReason.Add, key, u.Current);
                        }
                        else
                        {
                            if (exisiting.HasValue)
                            {
                                _cache.Remove(u.Key);
                                change = new Change<TObject, TKey>(ChangeReason.Remove, key, u.Current, exisiting);
                            }
                        }
                    }
                        break;
                    case ChangeReason.Remove:
                        if (exisiting.HasValue)
                        {
                            _cache.Remove(u.Key);
                            change = u; //new Change<TObject, TKey>(ChangeReason.Remove, key, u.Current, exisiting);
                        }

                        break;
                    case ChangeReason.Evaluate:
                        if (matches)
                        {
                            if (!exisiting.HasValue)
                            {
                                _cache.AddOrUpdate(u.Current, u.Key);
                                change = new Change<TObject, TKey>(ChangeReason.Add, key, u.Current);
                            }
                            else
                            {
                                change = u;
                            }
                        }
                        else
                        {
                            if (exisiting.HasValue)
                            {
                                _cache.Remove(u.Key);
                                change = new Change<TObject, TKey>(ChangeReason.Remove, key, u.Current, exisiting);
                            }
                        }
                        break;
                }
                if (change.HasValue)
                {
                    updates.Add(change.Value);
                }
            }
            return new ChangeSet<TObject, TKey>(updates);
        }

        protected struct UpdateWithFilter
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="T:System.Object"/> class.
            /// </summary>
            public UpdateWithFilter(bool isMatch, Change<TObject, TKey> change)
            {
                IsMatch = isMatch;
                Change = change;
            }

            public Change<TObject, TKey> Change { get; }
            public bool IsMatch { get; }
        }
    }
}
