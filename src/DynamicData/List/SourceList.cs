// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Annotations;
using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// An editable observable list
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    public sealed class SourceList<T> : ISourceList<T>
    {
        private readonly ISubject<IChangeSet<T>> _changes = new Subject<IChangeSet<T>>();
        private readonly Subject<IChangeSet<T>> _changesPreview = new Subject<IChangeSet<T>>();
        private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
        private readonly ReaderWriter<T> _readerWriter = new ReaderWriter<T>();
        private readonly IDisposable _cleanUp;
        private readonly object _locker = new object();
        private readonly object _writeLock = new object();
        private int _editLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceList{T}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public SourceList(IObservable<IChangeSet<T>> source = null)
        {
            var loader = source == null ? Disposable.Empty : LoadFromSource(source);

            _cleanUp = Disposable.Create(() =>
            {
                loader.Dispose();
                OnCompleted();
                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnCompleted();
                }
            });
        }

        private IDisposable LoadFromSource(IObservable<IChangeSet<T>> source)
        {
            return source
                .Finally(OnCompleted)
                .Select(_readerWriter.Write)
                .Subscribe(InvokeNext, OnError, OnCompleted);
        }

        /// <inheritdoc />
        public void Edit([NotNull] Action<IExtendedList<T>> updateAction)
        {
            if (updateAction == null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            lock (_writeLock)
            {
                IChangeSet<T> changes = null;

                _editLevel++;

                if (_editLevel == 1)
                {
                    if (_changesPreview.HasObservers)
                    {
                        changes = _readerWriter.WriteWithPreview(updateAction, InvokeNextPreview);
                    }
                    else
                    {
                        changes = _readerWriter.Write(updateAction);
                    }
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

        private void InvokeNextPreview(IChangeSet<T> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }

            lock (_locker)
            {
                _changesPreview.OnNext(changes);
            }
        }

        private void InvokeNext(IChangeSet<T> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }

            lock (_locker)
            {
                _changes.OnNext(changes);

                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnNext(_readerWriter.Count);
                }
            }
        }

        private void OnCompleted()
        {
            lock (_locker)
            {
                _changesPreview.OnCompleted();
                _changes.OnCompleted();
            }
        }

        private void OnError(Exception exception)
        {
            lock (_locker)
            {
                _changesPreview.OnError(exception);
                _changes.OnError(exception);
            }
        }

        /// <inheritdoc />
        public IEnumerable<T> Items => _readerWriter.Items;

        /// <inheritdoc />
        public int Count => _readerWriter.Count;

        /// <inheritdoc /> 
        public T this[int index]
        {
            get => _readerWriter[index];
            set
            {
                this.Edit((list) =>
                {
                    list[index] = value;
                });
            }
        }

        /// <inheritdoc />
        public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();

        /// <inheritdoc />
        public IObservable<IChangeSet<T>> Connect(Func<T, bool> predicate = null)
        {
            var observable = Observable.Create<IChangeSet<T>>(observer =>
            {
                lock (_locker)
                {
                    var initial = new ChangeSet<T>(new[] {new Change<T>(ListChangeReason.AddRange, _readerWriter.Items)});
                    if (initial.TotalChanges > 0)
                    {
                        observer.OnNext(initial);
                    }

                    var source = _changes.Finally(observer.OnCompleted);

                    return source.SubscribeSafe(observer);
                }
            });

            if (predicate != null)
            {
                observable = new FilterStatic<T>(observable, predicate).Run();
            }

            return observable;
        }

        /// <inheritdoc />
        public IObservable<IChangeSet<T>> Preview(Func<T, bool> predicate = null)
        {
            IObservable<IChangeSet<T>> observable = _changesPreview;

            if (predicate != null)
            {
                observable = new FilterStatic<T>(observable, predicate).Run();
            }

            return observable;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cleanUp.Dispose();
            _changesPreview?.Dispose();
        }

        /// <inheritdoc /> 
        public IEnumerator<T> GetEnumerator() => this.Items.GetEnumerator();

        /// <inheritdoc /> 
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        /// <inheritdoc /> 
        public int IndexOf(T item) => this._readerWriter.IndexOf(item);

        /// <inheritdoc /> 
        public bool Contains(T item) => this._readerWriter.Contains(item);

        /// <inheritdoc /> 
        public void CopyTo(T[] array, int arrayIndex) => this._readerWriter.CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public void Insert(int index, T item) => this.Edit(list => list.Insert(index, item));

        /// <inheritdoc />
        public void RemoveAt(int index) => this.Edit(list => list.RemoveAt(index));

        /// <inheritdoc />
        public void Add(T item) => this.Edit(list => list.Add(item));

        /// <inheritdoc />
        public void Clear() => this.Edit(list => list.Clear());

        /// <inheritdoc />
        public bool Remove(T item)
        {
            bool removed = false;
            this.Edit(list => removed = list.Remove(item));
            return removed;
        }

        bool ICollection<T>.IsReadOnly => false;
    }
}
