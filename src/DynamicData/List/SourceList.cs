// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// An editable observable list.
/// </summary>
/// <typeparam name="T">The type of the object.</typeparam>
[DebuggerDisplay("SourceList<{typeof(T).Name}> ({Count} Items)")]
public sealed class SourceList<T> : ISourceList<T>
    where T : notnull
{
    private readonly ISubject<IChangeSet<T>> _changes = new Subject<IChangeSet<T>>();

    private readonly Subject<IChangeSet<T>> _changesPreview = new();

    private readonly IDisposable _cleanUp;

    private readonly Lazy<ISubject<int>> _countChanged = new(() => new Subject<int>());

#if NET9_0_OR_GREATER
    private readonly Lock _locker = new();
#else
    private readonly object _locker = new();
#endif

    private readonly ReaderWriter<T> _readerWriter = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposal is superfluous after completion, and causes a bunch of test failures")]
    private readonly Lazy<BehaviorSubject<bool>> _isEditInProgress;

    private int _editLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceList{T}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public SourceList(IObservable<IChangeSet<T>>? source = null)
    {
        _isEditInProgress = new(() => new(_editLevel is not 0));

        var loader = source is null ? Disposable.Empty : LoadFromSource(source);

        _cleanUp = Disposable.Create(
            () =>
            {
                loader.Dispose();
                OnCompleted();
                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnCompleted();
                }
            });
    }

    /// <inheritdoc />
    public int Count => _readerWriter.Count;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IReadOnlyList<T> Items => _readerWriter.Items;

    /// <inheritdoc />
    public IObservable<IChangeSet<T>> Connect(Func<T, bool>? predicate = null)
        => Observable.Create<IChangeSet<T>>(observer =>
        {
            lock (_locker)
            {
                var observable = _isEditInProgress.IsValueCreated || (_editLevel is not 0)

                    // Defer connection until there is no longer an in-progress edit.
                    ? _isEditInProgress.Value
                        .Where(static isEditInProgress => !isEditInProgress)
                        .Take(1)
                        .SelectMany(_ => CreateConnectObservable(predicate))

                    // Otherwise, just connect immediately, and avoid forcing the edit-tracking system to initialize.
                    : CreateConnectObservable(predicate);

                return observable.SubscribeSafe(observer);
            }
        });

    /// <inheritdoc />
    public void Dispose()
    {
        _cleanUp.Dispose();
        _changesPreview.Dispose();
        // Intentionally skipping disposal for _isEditInProgress, as it's technically redundant after _cleanUp.Dispose()
        // calls .OnCompleted(), and doing disposal anyway causes a whole bunch of test failures. That really suggests
        // we need to rework the lifecycle mechanics of this class, as a whole, but that's probably going to involve
        // breaking changes.
    }

    /// <inheritdoc />
    public void Edit(Action<IExtendedList<T>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        lock (_locker)
        {
            IChangeSet<T>? changes = null;

            _editLevel++;
            if (_isEditInProgress.IsValueCreated && (_editLevel is 1))
                _isEditInProgress.Value.OnNext(true);
            try
            {
                try
                {
                    if (_editLevel == 1)
                    {
                        changes = _changesPreview.HasObservers ? _readerWriter.WriteWithPreview(updateAction, InvokeNextPreview) : _readerWriter.Write(updateAction);
                    }
                    else
                    {
                        _readerWriter.WriteNested(updateAction);
                    }
                }
                finally
                {
                    _editLevel--;
                }

                if (changes is not null && (_editLevel is 0))
                {
                    InvokeNext(changes);
                }
            }
            finally
            {
                if (_isEditInProgress.IsValueCreated && (_editLevel is 0))
                    _isEditInProgress.Value.OnNext(false);
            }
        }
    }

    /// <inheritdoc />
    public IObservable<IChangeSet<T>> Preview(Func<T, bool>? predicate = null)
    {
        IObservable<IChangeSet<T>> observable = _changesPreview;

        if (predicate is not null)
        {
            observable = new FilterStatic<T>(observable, predicate).Run();
        }

        return observable;
    }

    private IObservable<IChangeSet<T>> CreateConnectObservable(Func<T, bool>? predicate)
    {
        var observable = Observable.Create<IChangeSet<T>>(
            observer =>
            {
                lock (_locker)
                {
                    if (_readerWriter.Items.Length > 0)
                    {
                        observer.OnNext(
                            new ChangeSet<T>
                            {
                                new(ListChangeReason.AddRange, _readerWriter.Items, 0)
                            });
                    }

                    var source = _changes.Finally(observer.OnCompleted);

                    return source.SubscribeSafe(observer);
                }
            });

        if (predicate is not null)
        {
            observable = new FilterStatic<T>(observable, predicate).Run();
        }

        return observable;
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

    private IDisposable LoadFromSource(IObservable<IChangeSet<T>> source) => source.Synchronize(_locker).Finally(OnCompleted).Select(_readerWriter.Write).Subscribe(InvokeNext, OnError, OnCompleted);

    private void OnCompleted()
    {
        lock (_locker)
        {
            _changesPreview.OnCompleted();
            _changes.OnCompleted();
            if (_isEditInProgress.IsValueCreated)
                _isEditInProgress.Value.OnCompleted();
        }
    }

    private void OnError(Exception exception)
    {
        lock (_locker)
        {
            _changesPreview.OnError(exception);
            _changes.OnError(exception);
            if (_isEditInProgress.IsValueCreated)
                _isEditInProgress.Value.OnError(exception);
        }
    }
}
