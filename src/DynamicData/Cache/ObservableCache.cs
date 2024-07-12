// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData;

[DebuggerDisplay("ObservableCache<{typeof(TObject).Name}, {typeof(TKey).Name}> ({Count} Items)")]
internal sealed class ObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>, INotifyCollectionChangedSuspender
    where TObject : notnull
    where TKey : notnull
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<ChangeSet<TObject, TKey>> _changes = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly Subject<ChangeSet<TObject, TKey>> _changesPreview = new();

    private readonly IDisposable _cleanUp;

    private readonly Lazy<ISubject<int>> _countChanged = new(() => new Subject<int>());

    private readonly Lazy<SuspensionTracker> _suspensionTracker;

    private readonly object _locker = new();

    private readonly ReaderWriter<TObject, TKey> _readerWriter;

    private int _editLevel; // The level of recursion in editing.

    public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
    {
        _suspensionTracker = new(() => new SuspensionTracker(_changes.OnNext, InvokeCountNext));
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
                if (_suspensionTracker.IsValueCreated)
                {
                    _suspensionTracker.Value.Dispose();
                }

                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnCompleted();
                }
            });
    }

    public ObservableCache(Func<TObject, TKey>? keySelector = null)
    {
        _suspensionTracker = new(() => new SuspensionTracker(_changes.OnNext, InvokeCountNext));
        _readerWriter = new ReaderWriter<TObject, TKey>(keySelector);

        _cleanUp = Disposable.Create(
            () =>
            {
                _changes.OnCompleted();
                _changesPreview.OnCompleted();
                if (_suspensionTracker.IsValueCreated)
                {
                    _suspensionTracker.Value.Dispose();
                }

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

    public IReadOnlyList<TObject> Items => _readerWriter.Items;

    public IReadOnlyList<TKey> Keys => _readerWriter.Keys;

    public IReadOnlyDictionary<TKey, TObject> KeyValues => _readerWriter.KeyValues;

    public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool>? predicate = null, bool suppressEmptyChangeSets = true) =>
        Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            lock (_locker)
            {
                var observable = (!_suspensionTracker.IsValueCreated || !_suspensionTracker.Value.AreNotificationsSuspended)

                    // Create the Connection Observable
                    ? CreateConnectObservable(predicate, suppressEmptyChangeSets)

                    // Defer until notifications are no longer suspended
                    : _suspensionTracker.Value.NotificationsSuspendedObservable.Do(static _ => { }, observer.OnCompleted)
                        .Where(static b => !b).Take(1).Select(_ => CreateConnectObservable(predicate, suppressEmptyChangeSets)).Switch();

                return observable.SubscribeSafe(observer);
            }
        });

    public void Dispose() => _cleanUp.Dispose();

    public Optional<TObject> Lookup(TKey key) => _readerWriter.Lookup(key);

    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null) => predicate is null ? _changesPreview : _changesPreview.Filter(predicate);

    public IObservable<Change<TObject, TKey>> Watch(TKey key) =>
        Observable.Create<Change<TObject, TKey>>(observer =>
        {
            lock (_locker)
            {
                var observable = (!_suspensionTracker.IsValueCreated || !_suspensionTracker.Value.AreNotificationsSuspended)

                    // Create the Watch Observable
                    ? CreateWatchObservable(key)

                    // Defer until notifications are no longer suspended
                    : _suspensionTracker.Value.NotificationsSuspendedObservable.Do(static _ => { }, observer.OnCompleted)
                        .Where(static b => !b).Take(1).Select(_ => CreateWatchObservable(key)).Switch();

                return observable.SubscribeSafe(observer);
            }
        });

    public IDisposable SuspendCount()
    {
        lock (_locker)
        {
            _suspensionTracker.Value.SuspendCount();
            return Disposable.Create(this, static cache => cache.ResumeCount());
        }
    }

    public IDisposable SuspendNotifications()
    {
        lock (_locker)
        {
            _suspensionTracker.Value.SuspendNotifications();
            return Disposable.Create(this, static cache => cache.ResumeNotifications());
        }
    }

    internal ChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool>? filter = null) => _readerWriter.GetInitialUpdates(filter);

    internal void UpdateFromIntermediate(Action<ICacheUpdater<TObject, TKey>> updateAction)
    {
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

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
        updateAction.ThrowArgumentNullExceptionIfNull(nameof(updateAction));

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

    private IObservable<IChangeSet<TObject, TKey>> CreateConnectObservable(Func<TObject, bool>? predicate, bool suppressEmptyChangeSets) =>
        Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                lock (_locker)
                {
                    var initial = InternalEx.Return(() => (IChangeSet<TObject, TKey>)GetInitialUpdates(predicate));
                    var changes = initial.Concat(_changes);

                    if (predicate != null)
                    {
                        changes = changes.Filter(predicate, suppressEmptyChangeSets);
                    }
                    else if (suppressEmptyChangeSets)
                    {
                        changes = changes.NotEmpty();
                    }

                    return changes.SubscribeSafe(observer);
                }
            });

    private IObservable<Change<TObject, TKey>> CreateWatchObservable(TKey key) =>
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
                            foreach (var change in changes.ToConcreteType())
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

    private void InvokeNext(ChangeSet<TObject, TKey> changes)
    {
        lock (_locker)
        {
            // If Notifications are not suspended
            if (!_suspensionTracker.IsValueCreated || !_suspensionTracker.Value.AreNotificationsSuspended)
            {
                // Emit the changes
                _changes.OnNext(changes);
            }
            else
            {
                // Don't emit the changes, but add them to the list
                _suspensionTracker.Value.EnqueueChanges(changes);
            }

            // If CountChanges are not suspended
            if (!_suspensionTracker.IsValueCreated || !_suspensionTracker.Value.IsCountSuspended)
            {
                InvokeCountNext();
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

    private void InvokeCountNext()
    {
        lock (_locker)
        {
            if (_countChanged.IsValueCreated)
            {
                _countChanged.Value.OnNext(_readerWriter.Count);
            }
        }
    }

    private void ResumeCount()
    {
        lock (_locker)
        {
            Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Count without Suspend Count instance");
            _suspensionTracker.Value.ResumeCount();
        }
    }

    private void ResumeNotifications()
    {
        lock (_locker)
        {
            Debug.Assert(_suspensionTracker.IsValueCreated, "Should not be Resuming Notifications without Suspend Count instance");
            _suspensionTracker.Value.ResumeNotifications();
        }
    }

    private sealed class SuspensionTracker(Action<ChangeSet<TObject, TKey>> onResumeNotifications, Action onResumeCount) : IDisposable
    {
        private readonly BehaviorSubject<bool> _areNotificationsSuspended = new(false);

        private readonly Action<ChangeSet<TObject, TKey>> _onResumeNotifications = onResumeNotifications;

        private readonly Action _onResumeCount = onResumeCount;

        private List<Change<TObject, TKey>> _pendingChanges = [];

        private int _countSuspendCount;

        private int _notifySuspendCount;

        public bool IsCountSuspended => _countSuspendCount > 0;

        public bool AreNotificationsSuspended => _notifySuspendCount > 0;

        public IObservable<bool> NotificationsSuspendedObservable => _areNotificationsSuspended;

        public void EnqueueChanges(IEnumerable<Change<TObject, TKey>> changes)
        {
            Debug.Assert(changes is not null, "Don't pass in a null Enumerable");
            Debug.Assert(AreNotificationsSuspended, "Shouldn't be adding pending values if notifications aren't suspended");
            _pendingChanges.AddRange(changes);
        }

        public void SuspendNotifications()
        {
            if (++_notifySuspendCount == 1)
            {
                Debug.Assert(_pendingChanges.Count == 0, "Shouldn't be any pending values if suspend was just started");
                Debug.Assert(!_areNotificationsSuspended.Value, "SuspendSubject should be false for the first suspend call");
                _areNotificationsSuspended.OnNext(true);
            }
        }

        public void SuspendCount() => ++_countSuspendCount;

        public void ResumeNotifications()
        {
            if (--_notifySuspendCount == 0 && !_areNotificationsSuspended.IsDisposed)
            {
                // Fire pending changes to existing subscribers
                if (_pendingChanges.Count > 0)
                {
                    _onResumeNotifications(new ChangeSet<TObject, TKey>(_pendingChanges));
                    _pendingChanges.Clear();
                }

                // Tell deferred subscribers they can continue
                _areNotificationsSuspended.OnNext(false);
            }
        }

        public void ResumeCount()
        {
            if (--_countSuspendCount == 0)
            {
                _onResumeCount();
            }
        }

        public void Dispose()
        {
            _areNotificationsSuspended.OnCompleted();
            _areNotificationsSuspended.Dispose();
        }
    }
}
