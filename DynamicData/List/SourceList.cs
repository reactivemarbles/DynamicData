using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
        private readonly ReaderWriter<T> _readerWriter;
        private readonly IDisposable _cleanUp;
        private readonly object _locker = new object();
        private readonly object _writeLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceList{T}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public SourceList(IObservable<IChangeSet<T>> source = null)
        {
            _readerWriter = new ReaderWriter<T>();

            var loader = source == null ? Disposable.Empty : LoadFromSource(source);

            _cleanUp = Disposable.Create(() =>
            {
                loader.Dispose();
                OnCompleted();
                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnCompleted();
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
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            lock (_writeLock)
            {
                InvokeNext(_readerWriter.Write(updateAction));
            }
        }


        private void InvokeNext(IChangeSet<T> changes)
        {
            if (changes.Count == 0) return;

            lock (_locker)
            {
                _changes.OnNext(changes);

                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnNext(_readerWriter.Count);
            }
        }


        /// <inheritdoc />
        public void OnCompleted()
        {
            lock (_locker)
                _changes.OnCompleted();
        }

        /// <inheritdoc />
        public void OnError(Exception exception)
        {
            lock (_locker)
                _changes.OnError(exception);
        }

        /// <inheritdoc />
        public IEnumerable<T> Items => _readerWriter.Items;

        /// <inheritdoc />
        public int Count => _readerWriter.Count;

        /// <inheritdoc />
        public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();

        /// <inheritdoc />
        public IObservable<IChangeSet<T>> Connect(Func<T, bool> predicate = null)
        {
            var observable = Observable.Create<IChangeSet<T>>(observer =>
            {
                lock (_locker)
                {
                    var initial = GetInitialUpdates();
                    if (initial.TotalChanges > 0) observer.OnNext(initial);
                    var source = _changes.Finally(observer.OnCompleted);

                    return source.SubscribeSafe(observer);
                }
            });

            if (predicate != null)
                observable = observable.Filter(predicate);

            return observable;
        }

        private IChangeSet<T> GetInitialUpdates(Func<T, bool> predicate = null)
        {
            var items = predicate == null
                ? _readerWriter.Items
                : _readerWriter.Items.Where(predicate);

            return new ChangeSet<T>(new[] {new Change<T>(ListChangeReason.AddRange, items)});
        }
         

        /// <inheritdoc />
        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}
