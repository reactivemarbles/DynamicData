// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
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
        where TKey : notnull
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
        private readonly Subject<ChangeSet<TObject, TKey>> _changes = new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
        private readonly Subject<ChangeSet<TObject, TKey>> _changesPreview = new();

        private readonly IDisposable _cleanUp;

        private readonly Lazy<ISubject<int>> _countChanged = new(() => new Subject<int>());

        private readonly object _locker = new();

        private readonly ReaderWriter<TObject, TKey> _readerWriter;

        private int _editLevel; // The level of recursion in editing.

        public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>();

            var loader = source.Synchronize(_locker).Finally(
                () =>
                    {
                        _changes.OnCompleted();
                        _changesPreview.OnCompleted();
                    }).Subscribe(
                changeSet =>
                    {
                        var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                        var changes = _readerWriter.Write(changeSet, previewHandler, _changes.HasObservers);
                        InvokeNext(changes);
                    },
                ex =>
                    {
                        _changesPreview.OnError(ex);
                        _changes.OnError(ex);
                    });

            _cleanUp = Disposable.Create(
                () =>
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

        public ObservableCache(Func<TObject, TKey>? keySelector = null)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>(keySelector);

            _cleanUp = Disposable.Create(
                () =>
                    {
                        _changes.OnCompleted();
                        _changesPreview.OnCompleted();
                        if (_countChanged.IsValueCreated)
                        {
                            _countChanged.Value.OnCompleted();
                        }
                    });
        }

        public int Count => _readerWriter.Count;

        public IObservable<int> CountChanged =>
            Observable.Create<int>(
                observer =>
                    {
                        lock (_locker)
                        {
                            var source = _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();
                            return source.SubscribeSafe(observer);
                        }
                    });

        public IEnumerable<TObject> Items => _readerWriter.Items;

        public IEnumerable<TKey> Keys => _readerWriter.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _readerWriter.KeyValues;

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) =>
            Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                {
                    var initial = InternalEx.Return(() =>
                    {
                        // lock getting initial changes and rely on a combination of Concat
                        // + _changes being synchronized to produce thread safety  (I hope!)
                        lock (_locker)
                        {
                            return (IChangeSet<TObject, TKey>)GetInitialUpdates(predicate);
                        }
                    });

                    var changes = Observable.Defer(() => initial).Concat(_changes);
                    if (predicate != null)
                    {
                        changes = changes.Filter(predicate, suppressEmptyChangeSets);
                    }
                    else if (suppressEmptyChangeSets)
                    {
                        changes = changes.NotEmpty();
                    }

                    return changes.SubscribeSafe(observer);
                });

        public void Dispose() => _cleanUp.Dispose();

        public Optional<TObject> Lookup(TKey key) => _readerWriter.Lookup(key);

        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null)
        {
            return predicate is null ? _changesPreview : _changesPreview.Filter(predicate);
        }

        public IObservable<Change<TObject, TKey>> Watch(TKey key) =>
            Observable.Create<Change<TObject, TKey>>(
                observer =>
                    {
                        lock (_locker)
                        {
                            var initial = _readerWriter.Lookup(key);
                            if (initial.HasValue)
                            {
                                observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));
                            }

                            return _changes.Finally(observer.OnCompleted).Subscribe(
                                changes =>
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

        internal ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool>? filter = null) => _readerWriter.GetInitialUpdates(filter);

        internal void UpdateFromIntermediate(Action<ICacheUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction is null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            lock (_locker)
            {
                ChangeSet<TObject, TKey>? changes = null;

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

                if (changes is not null && _editLevel == 0)
                {
                    InvokeNext(changes);
                }
            }
        }

        internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (updateAction is null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            lock (_locker)
            {
                ChangeSet<TObject, TKey>? changes = null;

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

                if (changes is not null && _editLevel == 0)
                {
                    InvokeNext(changes);
                }
            }
        }

        private void InvokeNext(ChangeSet<TObject, TKey> changes)
        {
            lock (_locker)
            {
                _changes.OnNext(changes);

                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnNext(_readerWriter.Count);
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
    }
}