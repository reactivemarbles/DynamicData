// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Experimental;
#else

namespace DynamicData.Experimental;
#endif

/// <summary>
/// Provides members for the Watcher class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
internal sealed class Watcher<TObject, TKey> : IWatcher<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _disposer field.
    /// </summary>
    private readonly IDisposable _disposer;

    /// <summary>
    /// The _locker field.
    /// </summary>
    private readonly Lock _locker = new();

    /// <summary>
    /// The _source field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly IObservableCache<TObject, TKey> _source;

    /// <summary>
    /// The _subscribers field.
    /// </summary>
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed with _cleanUp")]
    private readonly IntermediateCache<SubjectWithRefCount<Change<TObject, TKey>>, TKey> _subscribers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Watcher{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="scheduler">The scheduler value.</param>
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
                        scheduler.Schedule(
                            state: (subject: subscriber.Value, update),
                            action: static (_, state) =>
                            {
                                state.subject.OnNext(state.update);
                                return Disposable.Empty;
                            });
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

    /// <summary>
    /// Executes the Dispose operation.
    /// </summary>
    public void Dispose() => _disposer.Dispose();

    /// <summary>
    /// Executes the Watch operation.
    /// </summary>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
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
                        subject = new SubjectWithRefCount<Change<TObject, TKey>>(new ReplaySignal<Change<TObject, TKey>>(1));

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
