// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    internal sealed class ObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        private readonly Subject<ChangeSet<TObject, TKey>> _changes = new Subject<ChangeSet<TObject, TKey>>();
        private readonly Subject<ChangeSet<TObject, TKey>> _changesPreview = new Subject<ChangeSet<TObject, TKey>>();
        private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
        private readonly ReaderWriter<TObject, TKey> _readerWriter;
        private readonly IDisposable _cleanUp;
        private readonly object _locker = new object();
        private readonly object _writeLock = new object();

        private int _editLevel; // The level of recursion in editing.

        public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>();

            var loader = source.Synchronize(_locker)
                .Finally(()=>
                {
                    _changes.OnCompleted();
                    _changesPreview.OnCompleted();
                })
                .Subscribe(changeset =>
                {
                    var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                    var changes = _readerWriter.Write(changeset, previewHandler, _changes.HasObservers);
                    InvokeNext(changes);
                }, ex =>
                {
                    _changesPreview.OnError(ex);
                    _changes.OnError(ex);
                });

            _cleanUp = Disposable.Create(() =>
            {
                loader.Dispose();
                _changes.OnCompleted();
                _changesPreview.OnCompleted();
                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnCompleted();
                }
            });
        }

        public ObservableCache(Func<TObject, TKey> keySelector = null, IEqualityComparer<TKey> keyEqualityComparer = null)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>(keySelector, keyEqualityComparer);

            _cleanUp = Disposable.Create(() =>
            {
                _changes.OnCompleted();
                _changesPreview.OnCompleted();
                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnCompleted();
                }
            });
        }

        internal void UpdateFromIntermediate(Action<ICacheUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction == null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            lock (_writeLock)
            {
                ChangeSet<TObject, TKey> changes = null;

                _editLevel++;
                if (_editLevel == 1)
                {
                    var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                    changes = _readerWriter.Write(updateAction, previewHandler, _changes.HasObservers);
                }
                else
                {
                    _readerWriter.WriteNested(updateAction);
                }

                _editLevel--;

                if (_editLevel == 0)
                {
                    InvokeNext(changes);
                }
            }
        }

        internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction == null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            lock (_writeLock)
            {
                ChangeSet<TObject, TKey> changes = null;

                _editLevel++;
                if (_editLevel == 1)
                {
                    var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                    changes = _readerWriter.Write(updateAction, previewHandler, _changes.HasObservers);
                }
                else
                {
                    _readerWriter.WriteNested(updateAction);
                }

                _editLevel--;

                if (_editLevel == 0)
                {
                    InvokeNext(changes);
                }
            }
        }

        private void InvokePreview(ChangeSet<TObject, TKey> changes)
        {
            lock (_locker)
            {
                if (changes.Count != 0)
                {
                    _changesPreview.OnNext(changes);
                }
            }
        }

        private void InvokeNext(ChangeSet<TObject, TKey> changes)
        {
            lock (_locker)
            {
                if (changes.Count != 0)
                {
                    _changes.OnNext(changes);
                }

                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnNext(_readerWriter.Count);
                }
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
                        {
                            observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));
                        }

                        return _changes.Finally(observer.OnCompleted).Subscribe(changes =>
                        {
                            foreach (var change in changes)
                            {
                                var match = EqualityComparer<TKey>.Default.Equals(change.Key, key);
                                if (match)
                                {
                                    observer.OnNext(change);
                                }
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

        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool> predicate = null)
        {
            return predicate == null ? _changesPreview : _changesPreview.Filter(predicate);
        }

        internal ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool> filter = null) => _readerWriter.GetInitialUpdates(filter);

        public Optional<TObject> Lookup(TKey key) => _readerWriter.Lookup(key);

        public IEnumerable<TKey> Keys => _readerWriter.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _readerWriter.KeyValues;

        public IEnumerable<TObject> Items => _readerWriter.Items;

        public int Count => _readerWriter.Count;

        public void Dispose() => _cleanUp.Dispose();
    }
}
