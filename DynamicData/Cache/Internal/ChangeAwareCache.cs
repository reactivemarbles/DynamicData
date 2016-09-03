using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;

namespace DynamicData.Internal
{

    internal class LockFreeObservableCache<TObject, TKey>: IObservableCache<TObject, TKey>
    {
        private readonly ChangeAwareCache<TObject, TKey> _innerCache = new ChangeAwareCache<TObject, TKey>();
        private readonly ISubject<IChangeSet<TObject, TKey>> _changes = new Subject<IChangeSet<TObject, TKey>>();
        private readonly ISubject<int> _countChanged = new Subject<int>();
        private readonly IDisposable _cleanUp;

        #region Observable methods

        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return Observable.Return(_innerCache.AsInitialUpdates()).Concat(_changes);           
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                            var filterer = new StaticFilter<TObject, TKey>(filter);
                            var filtered = filterer.Filter(_innerCache.AsInitialUpdates(filter));
                            if (filtered.Count != 0)
                                observer.OnNext(filtered);

                            return _changes
                                .Select(filterer.Filter)
                                .NotEmpty()
                                .SubscribeSafe(observer);
                    });
        }

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return Observable.Create<Change<TObject, TKey>>
            (
                observer =>
                {
                    var initial = _innerCache.Lookup(key);
                    if (initial.HasValue)
                        observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));

                    return _changes.Subscribe(changes =>
                    {
                        var matches = changes.Where(update => update.Key.Equals(key));
                        foreach (var match in matches)
                        {
                            observer.OnNext(match);
                        }
                    });
                });
        }

        public IObservable<int> CountChanged => _countChanged.StartWith(_innerCache.Count).DistinctUntilChanged();

        #endregion

        public LockFreeObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            var loader = source.Select(changes =>
            {
                _innerCache.Clone(changes);
                return _innerCache.CaptureChanges();
            }).SubscribeSafe(_changes);

            _cleanUp = Disposable.Create(() =>
            {
                loader.Dispose();
                _changes.OnCompleted();
                _countChanged.OnCompleted();
            });
        }

        public LockFreeObservableCache()
        {
            _cleanUp = Disposable.Create(() =>
            {
                _changes.OnCompleted();
                _countChanged.OnCompleted();
            });
        }

        public void Edit(Action<ChangeAwareCache<TObject, TKey>> editAction)
        {
            editAction(_innerCache);
            _changes.OnNext(_innerCache.CaptureChanges());
        }
        
        #region Accessors

        public Optional<TObject> Lookup(TKey key)
        {
            return _innerCache.Lookup(key);
        }

        public IEnumerable<TKey> Keys => _innerCache.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _innerCache.KeyValues;

        public IEnumerable<TObject> Items => _innerCache.Items;

        public int Count => _innerCache.Count;

        #endregion
        
        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }

    internal class ChangeAwareCache<TObject, TKey>
    {
        private List<Change<TObject, TKey>> _changes = new List<Change<TObject, TKey>>();
        
        private Dictionary<TKey, TObject> _data;

        public int Count => _data.Count;
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _data;
        public IEnumerable<TObject> Items => _data.Values;
        public IEnumerable<TKey> Keys => _data.Keys;

        public ChangeAwareCache()
        {
            _data = new Dictionary<TKey, TObject>();
        }


        public Optional<TObject> Lookup(TKey key)
        {
            return _data.Lookup(key);
        }

        public void AddOrUpdate(TObject item, TKey key)
        {
            TObject existingItem;

            _changes.Add(_data.TryGetValue(key, out existingItem)
                ? new Change<TObject, TKey>(ChangeReason.Update, key, item, existingItem)
                : new Change<TObject, TKey>(ChangeReason.Add, key, item));

            _data[key] = item;
        }

        public void Remove(IEnumerable<TKey> keys)
        {
            keys.ForEach(Remove);
        }

        public void Remove(TKey key)
        {
            TObject existingItem;
            if (_data.TryGetValue(key, out existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Remove, key, existingItem));
                _data.Remove(key);
            }
        }

        public void Evaluate()
        {
            _changes.Capacity = _data.Count + _changes.Count;
            _changes.AddRange(_data.Select(t => new Change<TObject, TKey>(ChangeReason.Evaluate, t.Key, t.Value)));
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            keys.ForEach(Evaluate);
        }

        public void Evaluate(TKey key)
        {
            TObject existingItem;
            if (_data.TryGetValue(key, out existingItem))
            {
                _changes.Add(new Change<TObject, TKey>(ChangeReason.Evaluate, key, existingItem));
            }
        }

        public void Clear()
        {
            var toremove = _data.Select(kvp => new Change<TObject, TKey>(ChangeReason.Remove, kvp.Key, kvp.Value)).ToArray();
            _changes.AddRange(toremove);
            _data.Clear();
        }

        public void Clone(IChangeSet<TObject, TKey> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            //for efficiency resize dictionary to initial batch size
            if (_data.Count == 0)
                _data = new Dictionary<TKey, TObject>(changes.Count);

            _changes.Capacity = changes.Count + _changes.Count;

            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                     case ChangeReason.Update:
                       AddOrUpdate(change.Current, change.Key);
                        break;
                    case ChangeReason.Remove:
                        Remove(change.Key);
                        break;
                    case ChangeReason.Evaluate:
                        Evaluate(change.Key);
                        break;
                }
            }
        }

        public ChangeSet<TObject, TKey> CaptureChanges()
        {
            var copy = new ChangeSet<TObject, TKey>(_changes);
            _changes = new List<Change<TObject, TKey>>();
            return copy;
        }
    }
}