using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Kernel
{
    internal class FilteredUpdater<TObject, TKey>
    {
        private readonly ICache<TObject, TKey> _cache;
        private readonly Func<TObject, bool> _filter;
        private readonly ParallelisationOptions _parallelisationOptions;

        public FilteredUpdater(ICache<TObject, TKey> cache, Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
        {
            if (cache == null) throw new ArgumentNullException("cache");
            if (parallelisationOptions == null) throw new ArgumentNullException("parallelisationOptions");

            _cache = cache;

            if (filter==null)
            {
                _filter = t => true;
            }
            else
            {
                _filter = filter;
            }
            _parallelisationOptions = parallelisationOptions;
        }

        public IEnumerable<TObject> Items
        {
            get { return _cache.Items; }
        }

        public IEnumerable<TKey> Keys
        {
            get { return _cache.Keys; }
        }

        public IEnumerable<KeyValuePair<TKey,TObject>> KeyValues
        {
            get { return _cache.KeyValues; }
        }

        public int Count
        {
            get { return _cache.Count; }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }


        public IChangeSet<TObject, TKey> Evaluate(IEnumerable<KeyValuePair<TKey,TObject>> items)
        {
            //this is an internal method only so we can be sure there are no duplicate keys in the result
            //(therefore safe to parallelise)
            Func<KeyValuePair<TKey,TObject>, Optional<Change<TObject, TKey>>> factory = kv =>
                {
                    var exisiting = _cache.Lookup(kv.Key);
                    var matches = _filter(kv.Value);
                    Optional<Change<TObject, TKey>> change = Optional.None<Change<TObject, TKey>>();

                    if (matches)
                    {
                        if (!exisiting.HasValue)
                        {
                            change = new Change<TObject, TKey>(ChangeReason.Add, kv.Key, kv.Value);
                        }
                    }
                    else
                    {
                        if (exisiting.HasValue)
                        {
                            change = new Change<TObject, TKey>(ChangeReason.Remove, kv.Key, kv.Value, exisiting);
       
                        }
                   }

                    return change;
                };

            var keyValues = items as KeyValuePair<TKey,TObject>[] ?? items.ToArray();
            
            var result = keyValues.ShouldParallelise(_parallelisationOptions)
                             ? keyValues.Parallelise(_parallelisationOptions).Select(factory).SelectValues()
                             : keyValues.Select(factory).SelectValues();

            var collection = new ChangeSet<TObject, TKey>(result);
            foreach (var update in collection)
            {
                switch (update.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                        _cache.AddOrUpdate(update.Current, update.Key);
                        break;
                    case ChangeReason.Remove:
                        _cache.Remove(update.Key);
                        break;
                }
            }
            return collection;

        }
        
        public IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates)
        {
            var withfilter = WithFilter(updates);
            return ProcessResult(withfilter);
        }


        private IEnumerable<UpdateWithFilter> WithFilter(IChangeSet<TObject, TKey> updates)
        {
            if (updates.ShouldParallelise(_parallelisationOptions))
            {
                return updates.Parallelise(_parallelisationOptions)
                           .Select(u => new UpdateWithFilter(_filter(u.Current), u)).ToArray();
            }
            return updates.Select(u => new UpdateWithFilter(_filter(u.Current), u)).ToArray();
        }

        private IChangeSet<TObject, TKey> ProcessResult(IEnumerable<UpdateWithFilter> result)
        {
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
                                _cache.AddOrUpdate(u.Current,u.Key);
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
                            change = new Change<TObject, TKey>(ChangeReason.Remove, key, u.Current, exisiting);
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

        private struct UpdateWithFilter
        {
            private readonly Change<TObject, TKey> _source;
            private readonly bool _isMatch;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:System.Object"/> class.
            /// </summary>
            public UpdateWithFilter(bool isMatch, Change<TObject, TKey> change)
            {
                _isMatch = isMatch;
                _source = change;
            }



            public Change<TObject, TKey> Change
            {
                get { return _source; }
            }

            public bool IsMatch
            {
                get { return _isMatch; }
            }
        }

    }
}