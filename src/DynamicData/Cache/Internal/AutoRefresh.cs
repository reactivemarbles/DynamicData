// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the AutoRefresh class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TAny">The type of the TAny value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="reEvaluator">The reEvaluator value.</param>
/// <param name="buffer">The buffer value.</param>
/// <param name="scheduler">The scheduler value.</param>
internal sealed class AutoRefresh<TObject, TKey, TAny>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TAny>> reEvaluator, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _reEvaluator field.
    /// </summary>
    private readonly Func<TObject, TKey, IObservable<TAny>> _reEvaluator = reEvaluator ?? throw new ArgumentNullException(nameof(reEvaluator));

    /// <summary>
    /// The _scheduler field.
    /// </summary>
    private readonly IScheduler _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var queue = new DeliveryQueue<IChangeSet<TObject, TKey>>(observer);
                var refreshes = new Signal<Change<TObject, TKey>>();
                var subscriptions = new Dictionary<TKey, ChildSubscription>();
                var locker = new object();
                var sourceCompleted = false;
                var activeSubscriptions = 0;
                var isProcessingSourceChange = false;
                List<Change<TObject, TKey>>? pendingRefreshes = null;

                IObservable<IChangeSet<TObject, TKey>> refreshChanges = buffer is null ?
                    refreshes.Select(c => new ChangeSet<TObject, TKey>(new[] { c })) :
                    refreshes.Buffer(buffer.Value, _scheduler).Where(list => list.Count > 0).Select(items => new ChangeSet<TObject, TKey>(items));

                var refreshSubscription = refreshChanges.SubscribeSafe(queue);

                var sourceSubscription = _source.Subscribe(
                    changes =>
                    {
                        try
                        {
                            lock (locker)
                            {
                                isProcessingSourceChange = true;

                                foreach (var change in changes)
                                {
                                    switch (change.Reason)
                                    {
                                        case ChangeReason.Add:
                                        case ChangeReason.Update:
                                            AddSubscription(change.Current, change.Key);
                                            break;

                                        case ChangeReason.Remove:
                                            RemoveSubscription(change.Key);
                                            break;
                                    }
                                }
                            }

                            queue.OnNext(changes);

                            List<Change<TObject, TKey>>? refreshesToEmit;
                            lock (locker)
                            {
                                isProcessingSourceChange = false;
                                refreshesToEmit = pendingRefreshes;
                                pendingRefreshes = null;
                            }

                            if (refreshesToEmit is not null)
                            {
                                foreach (var refresh in refreshesToEmit)
                                {
                                    refreshes.OnNext(refresh);
                                }
                            }
                        }
                        catch (Exception error)
                        {
                            lock (locker)
                            {
                                isProcessingSourceChange = false;
                                pendingRefreshes = null;
                            }

                            queue.OnError(error);
                        }
                    },
                    queue.OnError,
                    () =>
                    {
                        var shouldComplete = false;
                        lock (locker)
                        {
                            sourceCompleted = true;
                            shouldComplete = activeSubscriptions == 0;
                        }

                        if (shouldComplete)
                        {
                            refreshes.OnCompleted();
                        }
                    });

                return new CompositeDisposable(
                    queue,
                    sourceSubscription,
                    refreshSubscription,
                    Disposable.Create(
                        () =>
                        {
                            lock (locker)
                            {
                                foreach (var subscription in subscriptions.Values)
                                {
                                    subscription.Dispose();
                                }

                                subscriptions.Clear();
                            }
                        }),
                    refreshes);

                void AddSubscription(TObject item, TKey key)
                {
                    RemoveSubscription(key);

                    var observable = _reEvaluator(item, key);
                    var child = new ChildSubscription();
                    subscriptions[key] = child;
                    activeSubscriptions++;

                    var isSubscribing = true;

                    try
                    {
                        var subscription = observable.Subscribe(
                            onNext: _ =>
                            {
                                if (Volatile.Read(ref isSubscribing))
                                {
                                    return;
                                }

                                var refresh = new Change<TObject, TKey>(ChangeReason.Refresh, key, item);
                                var emitNow = false;

                                lock (locker)
                                {
                                    if (!child.IsActive || !subscriptions.TryGetValue(key, out var existingSubscription) || !ReferenceEquals(child, existingSubscription))
                                    {
                                        return;
                                    }

                                    if (isProcessingSourceChange)
                                    {
                                        pendingRefreshes ??= [];
                                        pendingRefreshes.Add(refresh);
                                    }
                                    else
                                    {
                                        emitNow = true;
                                    }
                                }

                                if (emitNow)
                                {
                                    refreshes.OnNext(refresh);
                                }
                            },
                            onError: _ => CompleteSubscription(key, child),
                            onCompleted: () => CompleteSubscription(key, child));

                        child.SetDisposable(subscription);
                    }
                    catch
                    {
                        RemoveSubscription(key);
                        throw;
                    }
                    finally
                    {
                        Volatile.Write(ref isSubscribing, false);
                    }
                }

                void RemoveSubscription(TKey key)
                {
#if NET8_0_OR_GREATER
                    if (!subscriptions.Remove(key, out var subscription))
#else
                    if (!subscriptions.TryGetValue(key, out var subscription) || !subscriptions.Remove(key))
#endif
                    {
                        return;
                    }

                    if (!subscription.TryDispose())
                    {
                        return;
                    }

                    activeSubscriptions--;
                    pendingRefreshes?.RemoveAll(change => EqualityComparer<TKey>.Default.Equals(change.Key, key));
                }

                void CompleteSubscription(TKey key, ChildSubscription subscription)
                {
                    var shouldComplete = false;

                    lock (locker)
                    {
                        if (!subscription.TryComplete())
                        {
                            return;
                        }

                        if (subscriptions.TryGetValue(key, out var existingSubscription) && ReferenceEquals(subscription, existingSubscription))
                        {
                            subscriptions.Remove(key);
                        }

                        activeSubscriptions--;
                        shouldComplete = sourceCompleted && activeSubscriptions == 0;
                    }

                    if (shouldComplete)
                    {
                        refreshes.OnCompleted();
                    }
                }
            });

    private sealed class ChildSubscription : IDisposable
    {
        private readonly SingleAssignmentDisposable _disposable = new();
        private int _isStopped;

        public bool IsActive => Volatile.Read(ref _isStopped) == 0;

        public void Dispose()
        {
            TryDispose();
            GC.SuppressFinalize(this);
        }

        public void SetDisposable(IDisposable disposable) => _disposable.Disposable = disposable;

        public bool TryComplete() => TryDispose();

        public bool TryDispose()
        {
            if (Interlocked.Exchange(ref _isStopped, 1) != 0)
            {
                return false;
            }

            _disposable.Dispose();
            return true;
        }
    }
}
