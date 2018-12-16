using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal sealed class ObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>, ICollectionSubject
    {
        private readonly Subject<ChangeSet<TObject, TKey>> _changes = new Subject<ChangeSet<TObject, TKey>>();
        private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
        private readonly ReaderWriter<TObject, TKey> _readerWriter;
        private readonly IDisposable _cleanUp;

        private readonly object _locker = new object();
        private readonly object _writeLock = new object();

        public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>();

            var loader = source
                .Synchronize(_locker)
                .Select(changes => _readerWriter.Write(changes, _changes.HasObservers))
                .Finally(_changes.OnCompleted)
                .Subscribe(InvokeNext,_changes.OnError);

            _cleanUp = Disposable.Create(() =>
            {
                loader.Dispose();
                _changes.OnCompleted();
                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnCompleted();
            });
        }

        public ObservableCache(Func<TObject, TKey> keySelector = null)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>(keySelector);

            _cleanUp = Disposable.Create(() =>
            {
                _changes.OnCompleted();
                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnCompleted();
            });
        }

        internal void UpdateFromIntermediate(Action<ICacheUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            lock (_writeLock)
            {
                InvokeNext(_readerWriter.Write(updateAction, _changes.HasObservers));
            }
        }

        internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            lock (_writeLock)
            {
                InvokeNext(_readerWriter.Write(updateAction, _changes.HasObservers));
            }
        }

        private void InvokeNext(ChangeSet<TObject, TKey> changes)
        {
            lock (_locker)
            {
                if (changes.Count != 0)
                    _changes.OnNext(changes);

                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnNext(_readerWriter.Count);
            }
        }

        public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return Observable.Create<Change<TObject, TKey>>
            (
                observer =>
                {
                    lock (_locker)
                    {
                        var initial = _readerWriter.Lookup(key);
                        if (initial.HasValue)
                            observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));

                        return _changes.Finally(observer.OnCompleted).Subscribe(changes =>
                        {
                            foreach (var change in changes)
                            {
                                var match = EqualityComparer<TKey>.Default.Equals(change.Key, key);
                                if (match)
                                    observer.OnNext(change);
                            }
                        });
                    }
                });
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            return Observable.Defer(() =>
            {
                lock (_locker)
                {
                    var initial = GetInitialUpdates(predicate);
                    var changes = Observable.Return(initial).Concat(_changes);

                    return (predicate == null ? changes : changes.Filter(predicate)).NotEmpty();
                }
            });
        }

        internal ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool> filter = null) => _readerWriter.GetInitialUpdates(filter);

        public Optional<TObject> Lookup(TKey key) => _readerWriter.Lookup(key);

        public IEnumerable<TKey> Keys => _readerWriter.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _readerWriter.KeyValues;

        public IEnumerable<TObject> Items => _readerWriter.Items;

        public int Count => _readerWriter.Count;

        public void Dispose()
        {
            _cleanUp.Dispose();
        }

        void ICollectionSubject.OnCompleted()
        {
            lock (_locker)
                _changes.OnCompleted();
        }
        
        void ICollectionSubject.OnError(Exception exception)
        {
            lock (_locker)
                _changes.OnError(exception);
        }
    }
}
