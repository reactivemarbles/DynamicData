// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Kernel;

namespace DynamicData.Experimental;

internal sealed class Watcher<TObject, TKey> : IWatcher<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IDisposable _disposer;

    private readonly object _locker = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly IObservableCache<TObject, TKey> _source;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly IntermediateCache<SubjectWithRefCount<Change<TObject, TKey>>, TKey> _subscribers = new();

    public Watcher(IObservable<IChangeSet<TObject, TKey>> source, IScheduler scheduler)
    {
        _source = source.AsObservableCache();

        var onCompletePublisher = _subscribers.Connect().Synchronize(_locker).ObserveOn(scheduler).SubscribeMany((t, _) => Disposable.Create(t.OnCompleted)).Subscribe();

        var sourceSubscriber = source.Synchronize(_locker).Subscribe(
            updates => updates.ForEach(
                update =>
                {
                    var subscriber = _subscribers.Lookup(update.Key);
                    if (subscriber.HasValue)
                    {
                        scheduler.Schedule(() => subscriber.Value.OnNext(update));
                    }
                }));

        _disposer = Disposable.Create(
            () =>
            {
                onCompletePublisher.Dispose();
                sourceSubscriber.Dispose();

                _source.Dispose();
                _subscribers.Dispose();
            });
    }

    public void Dispose() => _disposer.Dispose();

    public IObservable<Change<TObject, TKey>> Watch(TKey key) => Observable.Create<Change<TObject, TKey>>(
            observer =>
            {
                lock (_locker)
                {
                    // Create or find the existing subscribers
                    var existing = _subscribers.Lookup(key);
                    SubjectWithRefCount<Change<TObject, TKey>> subject;
                    if (existing.HasValue)
                    {
                        subject = existing.Value;
                    }
                    else
                    {
                        subject = new SubjectWithRefCount<Change<TObject, TKey>>(new ReplaySubject<Change<TObject, TKey>>(1));

                        var initial = _source.Lookup(key);
                        if (initial.HasValue)
                        {
                            var update = new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value);
                            subject.OnNext(update);
                        }

                        _subscribers.Edit(updater => updater.AddOrUpdate(subject, key));
                    }

                    // set up subscription
                    var subscriber = subject.SubscribeSafe(observer);

                    return Disposable.Create(
                        () =>
                        {
                            // lock to ensure no race condition where the same key could be subscribed
                            // to whilst disposal is taking place
                            lock (_locker)
                            {
                                subscriber.Dispose();
                                if (subject.RefCount == 0)
                                {
                                    _subscribers.Remove(key);
                                }
                            }
                        });
                }
            });
}
