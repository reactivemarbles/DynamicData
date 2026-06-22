// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// An editable observable list.
/// </summary>
/// <typeparam name="T">The type of the object.</typeparam>
[DebuggerDisplay("SourceList<{typeof(T).Name}> ({Count} Items)")]
public sealed class SourceList<T> : ISourceList<T>
    where T : notnull
{
    /// <summary>
    /// The _changes field.
    /// </summary>
    private readonly Signal<IChangeSet<T>> _changes = new();

    /// <summary>
    /// The _changesPreview field.
    /// </summary>
    private readonly Signal<IChangeSet<T>> _changesPreview = new();

    /// <summary>
    /// The _cleanUp field.
    /// </summary>
    private readonly IDisposable _cleanUp;

    /// <summary>
    /// The _countChanged field.
    /// </summary>
    private readonly Lazy<Signal<int>> _countChanged = new(() => new Signal<int>());

    /// <summary>
    /// The _locker field.
    /// </summary>
    private readonly Lock _locker = new();

    /// <summary>
    /// The _readerWriter field.
    /// </summary>
    private readonly ReaderWriter<T> _readerWriter = new();

    /// <summary>
    /// The _notifications field.
    /// </summary>
    private readonly DeliveryQueue<ListUpdate> _notifications;

    /// <summary>
    /// The _editLevel field.
    /// </summary>
    private int _editLevel;

    /// <summary>
    /// The _currentVersion field.
    /// </summary>
    private long _currentVersion;

    /// <summary>
    /// The _currentDeliveryVersion field.
    /// </summary>
    private long _currentDeliveryVersion;

    /// <summary>
    /// The _isDisposed field.
    /// </summary>
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceList{T}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public SourceList(IObservable<IChangeSet<T>>? source = null)
    {
        _notifications = new DeliveryQueue<ListUpdate>(_locker, new ListUpdateObserver(this));

        var loader = source is null ? Disposable.Empty : LoadFromSource(source);

        _cleanUp = Disposable.Create(
            () =>
            {
                loader.Dispose();
                NotifyCompleted();
            });
    }

    /// <inheritdoc />
    public int Count => _readerWriter.Count;

    /// <inheritdoc />
    public IObservable<int> CountChanged =>
        Observable.Create<int>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                if (_isDisposed)
                {
                    observer.OnNext(_readerWriter.Count);
                    observer.OnCompleted();
                    return Disposable.Empty;
                }

                var snapshotVersion = _currentVersion;
                var countChanged = readLock.HasPending
                    ? _countChanged.Value.SkipWhile(_ => Volatile.Read(ref _currentDeliveryVersion) <= snapshotVersion)
                    : _countChanged.Value;

                var source = countChanged.StartWith(_readerWriter.Count).DistinctUntilChanged();
                return source.SubscribeSafe(observer);
            });

    /// <inheritdoc />
    public IReadOnlyList<T> Items => _readerWriter.Items;

    /// <inheritdoc />
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Connect(Func<T, bool>? predicate = null)
    {
        var observable = Observable.Create<IChangeSet<T>>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                if (_readerWriter.Items.Length > 0)
                {
                    observer.OnNext(
                        new ChangeSet<T>
                        {
                            new(ListChangeReason.AddRange, _readerWriter.Items, 0)
                        });
                }

                if (_isDisposed)
                {
                    observer.OnCompleted();
                    return Disposable.Empty;
                }

                var snapshotVersion = _currentVersion;
                var changes = readLock.HasPending
                    ? _changes.SkipWhile(_ => Volatile.Read(ref _currentDeliveryVersion) <= snapshotVersion)
                    : (IObservable<IChangeSet<T>>)_changes;

                var source = changes.Finally(observer.OnCompleted);

                return source.SubscribeSafe(observer);
            });

        if (predicate is not null)
        {
            observable = new FilterStatic<T>(observable, predicate).Run();
        }

        return observable;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using (var notifications = _notifications.AcquireLock())
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
        }

        _cleanUp.Dispose();

        if (_notifications.IsDeliveringOnCurrentThread)
        {
            return;
        }

        SpinWait spinner = default;
        while (!_notifications.IsTerminated)
        {
            spinner.SpinOnce();
        }

        _notifications.Dispose();
        _changesPreview.Dispose();
        _changes.Dispose();
        if (_countChanged.IsValueCreated)
        {
            _countChanged.Value.Dispose();
        }
    }

    /// <inheritdoc />
    /// <param name="updateAction">The updateAction value.</param>
    public void Edit(Action<IExtendedList<T>> updateAction)
    {
        ArgumentExceptionHelper.ThrowIfNull(updateAction);

        using var notifications = _notifications.AcquireLock();

        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(SourceList<T>));
        }

        IChangeSet<T>? changes = null;

        _editLevel++;

        if (_editLevel == 1)
        {
            changes = _changesPreview.HasObservers ? _readerWriter.WriteWithPreview(updateAction, InvokeNextPreview) : _readerWriter.Write(updateAction);
        }
        else
        {
            _readerWriter.WriteNested(updateAction);
        }

        _editLevel--;

        if (changes is not null && changes.Count != 0 && _editLevel == 0)
        {
            notifications.EnqueueNext(new ListUpdate(changes, _readerWriter.Count, ++_currentVersion));
        }
    }

    /// <inheritdoc />
    /// <param name="predicate">The predicate value.</param>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Preview(Func<T, bool>? predicate = null)
    {
        var observable = Observable.Create<IChangeSet<T>>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                if (_isDisposed)
                {
                    observer.OnCompleted();
                    return Disposable.Empty;
                }

                return _changesPreview.SubscribeSafe(observer);
            });

        if (predicate is not null)
        {
            observable = new FilterStatic<T>(observable, predicate).Run();
        }

        return observable;
    }

    /// <summary>
    /// Executes the InvokeNextPreview operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    private void InvokeNextPreview(IChangeSet<T> changes)
    {
        if (changes.Count == 0 || _notifications.IsTerminated)
        {
            return;
        }

        lock (_locker)
        {
            _changesPreview.OnNext(changes);
        }
    }

    /// <summary>
    /// Executes the LoadFromSource operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <returns>The result of the operation.</returns>
    private IDisposable LoadFromSource(IObservable<IChangeSet<T>> source) =>
        source.Subscribe(
            changes =>
            {
                using var notifications = _notifications.AcquireLock();

                if (_isDisposed)
                {
                    return;
                }

                var capturedChanges = _readerWriter.Write(changes);
                if (capturedChanges.Count != 0)
                {
                    notifications.EnqueueNext(new ListUpdate(capturedChanges, _readerWriter.Count, ++_currentVersion));
                }
            },
            NotifyError,
            NotifyCompleted);

    /// <summary>
    /// Executes the NotifyCompleted operation.
    /// </summary>
    private void NotifyCompleted()
    {
        using var notifications = _notifications.AcquireLock();
        notifications.EnqueueCompleted();
    }

    /// <summary>
    /// Executes the NotifyError operation.
    /// </summary>
    /// <param name="exception">The exception value.</param>
    private void NotifyError(Exception exception)
    {
        using var notifications = _notifications.AcquireLock();
        notifications.EnqueueError(exception);
    }

    /// <summary>
    /// The notification payload for list delivery. Null Changes = count-only notification.
    /// </summary>
    /// <param name="Changes">The Changes value.</param>
    /// <param name="Count">The Count value.</param>
    /// <param name="Version">The Version value.</param>
    private readonly record struct ListUpdate(IChangeSet<T>? Changes, int Count, long Version = 0);

    /// <summary>
    /// Observer that dispatches <see cref="ListUpdate"/> items to the list's downstream subjects.
    /// </summary>
    /// <param name="sourceList">The source list value.</param>
    private sealed class ListUpdateObserver(SourceList<T> sourceList) : IObserver<ListUpdate>
    {
        /// <summary>
        /// Executes the OnNext operation.
        /// </summary>
        /// <param name="value">The value value.</param>
        public void OnNext(ListUpdate value)
        {
            if (value.Changes is not null)
            {
                Volatile.Write(ref sourceList._currentDeliveryVersion, value.Version);
                sourceList._changes.OnNext(value.Changes);
            }

            if (sourceList._countChanged.IsValueCreated)
            {
                sourceList._countChanged.Value.OnNext(value.Count);
            }
        }

        /// <summary>
        /// Executes the OnError operation.
        /// </summary>
        /// <param name="error">The error value.</param>
        public void OnError(Exception error)
        {
            sourceList._changesPreview.OnError(error);
            sourceList._changes.OnError(error);

            if (sourceList._countChanged.IsValueCreated)
            {
                sourceList._countChanged.Value.OnError(error);
            }
        }

        /// <summary>
        /// Executes the OnCompleted operation.
        /// </summary>
        public void OnCompleted()
        {
            sourceList._changesPreview.OnCompleted();
            sourceList._changes.OnCompleted();

            if (sourceList._countChanged.IsValueCreated)
            {
                sourceList._countChanged.Value.OnCompleted();
            }
        }
    }
}
