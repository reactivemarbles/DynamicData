using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal abstract class AbstractFilter<TObject, TKey> : IFilter<TObject, TKey>
    {
        private readonly ChangeAwareCache<TObject, TKey> _cache;

        protected AbstractFilter(ChangeAwareCache<TObject, TKey> cache, Func<TObject, bool> filter)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _cache = cache;

            if (filter == null)
            {
                Filter = t => true;
            }
            else
            {
                Filter = filter;
            }
        }

        public Func<TObject, bool> Filter { get; }

        public IChangeSet<TObject, TKey> Evaluate(IEnumerable<KeyValuePair<TKey, TObject>> items)
        {
            //this is an internal method only so we can be sure there are no duplicate keys in the result
            //(therefore safe to parallelise)
            Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory = kv =>
            {
                var exisiting = _cache.Lookup(kv.Key);
                var matches = Filter(kv.Value);

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
            _cache.Clone(new ChangeSet<TObject, TKey>(result));


            return _cache.CaptureChanges();
        }

        protected abstract IEnumerable<Change<TObject, TKey>> Evaluate(IEnumerable<KeyValuePair<TKey, TObject>> items, Func<KeyValuePair<TKey, TObject>, Optional<Change<TObject, TKey>>> factory);

        public IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates)
        {
            var withfilter = GetChangesWithFilter(updates);
            return ProcessResult(withfilter);
        }

        protected abstract IEnumerable<UpdateWithFilter> GetChangesWithFilter(IChangeSet<TObject, TKey> updates);

        private IChangeSet<TObject, TKey> ProcessResult(IEnumerable<UpdateWithFilter> source)
        {
            var result = source.AsArray();

            //Have to process one item at a time as an item can be included multiple
            //times in any batch

            foreach (var item in result)
            {
                var matches = item.IsMatch;
                var key = item.Change.Key;
                var u = item.Change;

                switch (item.Change.Reason)
                {
                    case ChangeReason.Add:
                        {
                            if (matches)
                                _cache.AddOrUpdate(u.Current, u.Key);
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
                    case ChangeReason.Evaluate:
                        {
                            var exisiting = _cache.Lookup(key);
                            if (matches)
                            {
                                if (!exisiting.HasValue)
                                {
                                    _cache.AddOrUpdate(u.Current, u.Key);
                                }
                                else
                                {
                                    _cache.Evaluate();
                                }
                            }
                            else
                            {
                                if (exisiting.HasValue)
                                    _cache.Remove(u.Key);
                            }
                        }
                        break;
                }
            }
            return _cache.CaptureChanges();
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
