using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    public class LockFreeObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        private readonly ChangeAwareCache<TObject, TKey> _innerCache = new ChangeAwareCache<TObject, TKey>();
        private readonly ICacheUpdater<TObject, TKey> _updater;
        private readonly ISubject<IChangeSet<TObject, TKey>> _changes = new Subject<IChangeSet<TObject, TKey>>();
        private readonly ISubject<int> _countChanged = new Subject<int>();
        private readonly IDisposable _cleanUp;

        public LockFreeObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _updater = new CacheUpdater<TObject, TKey>(_innerCache);

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
            _updater = new CacheUpdater<TObject, TKey>(_innerCache);

            _cleanUp = Disposable.Create(() =>
            {
                _changes.OnCompleted();
                _countChanged.OnCompleted();
            });
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            return Observable.Create<IChangeSet<TObject, TKey>>
            (
                observer =>
                {
                    if (predicate == null)
                    {
                        return Observable.Return(_innerCache.AsInitialUpdates())
                            .Concat(_changes)
                            .SubscribeSafe(observer);
                    }

                    var updater = new FilteredUpdater<TObject, TKey>(new ChangeAwareCache<TObject, TKey>(), predicate);
                    var filtered = updater.Update(_innerCache.AsInitialUpdates(predicate));
                    if (filtered.Count != 0)
                        observer.OnNext(filtered);

                    return _changes
                        .Select(updater.Update)
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

        public void Edit(Action<ICacheUpdater<TObject, TKey>> editAction)
        {
            editAction(_updater);
            _changes.OnNext(_innerCache.CaptureChanges());
        }

        public IObservable<int> CountChanged => _countChanged.StartWith(_innerCache.Count).DistinctUntilChanged();


        public Optional<TObject> Lookup(TKey key)
        {
            return _innerCache.Lookup(key);
        }

        public IEnumerable<TKey> Keys => _innerCache.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _innerCache.KeyValues;

        public IEnumerable<TObject> Items => _innerCache.Items;

        public int Count => _innerCache.Count;

        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}