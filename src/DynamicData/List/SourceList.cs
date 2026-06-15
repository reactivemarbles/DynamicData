// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

using DynamicData.Internal;
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<IChangeSet<T>> _changes = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<IChangeSet<T>> _changesPreview = new();

    private readonly IDisposable _cleanUp;

    private readonly Lazy<ISubject<int>> _countChanged = new(() => new Subject<int>());

#if NET9_0_OR_GREATER
    private readonly Lock _locker = new();
#else
    private readonly object _locker = new();
#endif

    private readonly ReaderWriter<T> _readerWriter = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Terminated via NotifyCompleted in _cleanUp")]
    private readonly DeliveryQueue<ListUpdate> _notifications;

    private int _editLevel;

    private long _currentVersion;

    private long _currentDeliveryVersion;

    // Set true (under the queue gate) the instant a terminal notification is enqueued, so any
    // Edit that lands between enqueue and the drain dequeueing the terminal can suppress its
    // preview emission. Without this, a preview subscriber would observe a change whose main
    // delivery is cleared from the queue when the terminal item is staged.
    private bool _terminalEnqueued;

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
    public IObservable<IChangeSet<T>> Connect(Func<T, bool>? predicate = null)
    {
        var observable = Observable.Create<IChangeSet<T>>(
            observer =>
            {
                using var readLock = _notifications.AcquireReadLock();

                var snapshot = _readerWriter.Items;
                var snapshotVersion = _currentVersion;

                var changes = readLock.HasPending
                    ? _changes.SkipWhile(_ => Volatile.Read(ref _currentDeliveryVersion) <= snapshotVersion)
                    : (IObservable<IChangeSet<T>>)_changes;

                IObservable<IChangeSet<T>> result;
                if (snapshot.Length > 0)
                {
                    var initial = new ChangeSet<T> { new(ListChangeReason.AddRange, snapshot, 0) };
                    result = Observable.Return((IChangeSet<T>)initial).Concat(changes);
                }
                else
                {
                    result = changes;
                }

                return result.Finally(observer.OnCompleted).SubscribeSafe(observer);
            });

        if (predicate is not null)
        {
            observable = new FilterStatic<T>(observable, predicate).Run();
        }

        return observable;
    }

    /// <inheritdoc />
    public void Dispose() => _cleanUp.Dispose();

    /// <inheritdoc />
    public void Edit(Action<IExtendedList<T>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

        using var notifications = _notifications.AcquireLock();

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

        if (changes is not null && changes.Count > 0 && _editLevel == 0)
        {
            notifications.EnqueueNext(new ListUpdate(changes, _readerWriter.Count, ++_currentVersion));
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

    private void InvokeNextPreview(IChangeSet<T> changes)
    {
        if (changes.Count != 0 && !_terminalEnqueued && !_notifications.IsTerminated)
        {
            _changesPreview.OnNext(changes);
        }
    }

    private IDisposable LoadFromSource(IObservable<IChangeSet<T>> source) =>
        source.Subscribe(
            changeSet =>
            {
                using var notifications = _notifications.AcquireLock();

                var changes = _readerWriter.Write(changeSet);

                if (changes.Count > 0)
                {
                    notifications.EnqueueNext(new ListUpdate(changes, _readerWriter.Count, ++_currentVersion));
                }
            },
            NotifyError,
            NotifyCompleted);

    private void NotifyCompleted()
    {
        using var notifications = _notifications.AcquireLock();
        _terminalEnqueued = true;
        notifications.EnqueueCompleted();
    }

    private void NotifyError(Exception exception)
    {
        using var notifications = _notifications.AcquireLock();
        _terminalEnqueued = true;
        notifications.EnqueueError(exception);
    }

    private readonly record struct ListUpdate(IChangeSet<T> Changes, int Count, long Version);

    private sealed class ListUpdateObserver(SourceList<T> source) : IObserver<ListUpdate>
    {
        public void OnNext(ListUpdate value)
        {
            Volatile.Write(ref source._currentDeliveryVersion, value.Version);
            source._changes.OnNext(value.Changes);

            if (source._countChanged.IsValueCreated)
            {
                source._countChanged.Value.OnNext(value.Count);
            }
        }

        public void OnError(Exception error)
        {
            source._changesPreview.OnError(error);
            source._changes.OnError(error);

            if (source._countChanged.IsValueCreated)
            {
                source._countChanged.Value.OnError(error);
            }
        }

        public void OnCompleted()
        {
            source._changes.OnCompleted();
            source._changesPreview.OnCompleted();

            if (source._countChanged.IsValueCreated)
            {
                source._countChanged.Value.OnCompleted();
            }
        }
    }
}
