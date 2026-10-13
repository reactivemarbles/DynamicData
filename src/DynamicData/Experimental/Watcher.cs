// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Experimental;

internal sealed class Watcher<TObject, TKey> : IWatcher<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IDisposable _disposer;

    private readonly Lock _locker = new();

    // Kept by the same subscription that hands out the changes, under the same lock, so a new watch's first value is
    // never out of step with the changes already handed out.
    private readonly Cache<TObject, TKey> _source = new();

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly IntermediateCache<SubjectWithRefCount<Change<TObject, TKey>>, TKey> _subscribers = new();

    // Applies the source's terminal event to a watch's subject, so that a subject created after the source has ended
    // starts out ended. Does nothing until then.
    private Action<SubjectWithRefCount<Change<TObject, TKey>>> _terminate = static _ => { };

    public Watcher(IObservable<IChangeSet<TObject, TKey>> source, IScheduler scheduler)
    {
        var onCompletePublisher = _subscribers.Connect().Synchronize(_locker).ObserveOn(scheduler).SubscribeMany((t, _) => Disposable.Create(t.OnCompleted)).Subscribe();

        var sourceSubscriber = source.Synchronize(_locker).Subscribe(
            updates =>
            {
                _source.Clone(updates);
                updates.ForEach(
                    update =>
                    {
                        var subscriber = _subscribers.Lookup(update.Key);
                        if (subscriber.HasValue)
                        {
                            scheduler.Schedule(() => subscriber.Value.OnNext(update));
                        }
                    });
            },
            error => Terminate(subject => subject.OnError(error)),
            () => Terminate(static subject => subject.OnCompleted()));

        _disposer = Disposable.Create(
            () =>
            {
                onCompletePublisher.Dispose();
                sourceSubscriber.Dispose();

                _subscribers.Dispose();
            });

        // The terminal event reaches existing watches through the scheduler, the same way their changes do.
        void Terminate(Action<SubjectWithRefCount<Change<TObject, TKey>>> terminate)
        {
            _terminate = terminate;
            _subscribers.Items.ForEach(subject => scheduler.Schedule(() => terminate(subject)));
        }
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

                        _terminate(subject);
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
