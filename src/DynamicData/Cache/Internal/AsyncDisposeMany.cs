// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

#if SUPPORTS_ASYNC_DISPOSABLE
internal static class AsyncDisposeMany<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public static IObservable<IChangeSet<TObject, TKey>> Create(
        IObservable<IChangeSet<TObject, TKey>> source,
        Action<IObservable<Unit>> disposalsCompletedAccessor)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(disposalsCompletedAccessor);

        return Observable
            .Create<IChangeSet<TObject, TKey>>(downstreamObserver =>
            {
                var gate = new object();
                var itemsByKey = new Dictionary<TKey, TObject>();
                var disposalsCompleted = new ReplaySignal<Unit>(1);
                var pendingAsyncDisposals = 0;
                var teardownStarted = false;
                var teardownCompleted = false;
                var disposalsFinalized = false;

                disposalsCompletedAccessor.Invoke(disposalsCompleted);

                var sourceSubscription = source
                    .SynchronizeSafe()
                    .SubscribeSafe(
                        onNext: upstreamChanges =>
                        {
                            downstreamObserver.OnNext(upstreamChanges);

                            foreach (var change in upstreamChanges.ToConcreteType())
                            {
                                switch (change.Reason)
                                {
                                    case ChangeReason.Update:
                                        if (change.Previous.HasValue && !EqualityComparer<TObject>.Default.Equals(change.Current, change.Previous.Value))
                                            TryDisposeItem(change.Previous.Value);
                                        break;

                                    case ChangeReason.Remove:
                                        TryDisposeItem(change.Current);
                                        break;
                                }
                            }

                            itemsByKey.Clone(upstreamChanges);
                        },
                        onError: error =>
                        {
                            downstreamObserver.OnError(error);
                            TearDown();
                        },
                        onCompleted: () =>
                        {
                            downstreamObserver.OnCompleted();
                            TearDown();
                        });

                return Disposable.Create(() =>
                {
                    sourceSubscription.Dispose();
                    TearDown();
                });

                void TearDown()
                {
                    TObject[] items;

                    lock (gate)
                    {
                        if (teardownStarted)
                            return;

                        teardownStarted = true;
                        items = [.. itemsByKey.Values];
                        itemsByKey.Clear();
                    }

                    try
                    {
                        foreach (var item in items)
                            TryDisposeItem(item);
                    }
                    catch (Exception error)
                    {
                        TryFailDisposals(error);
                    }

                    var publishCompleted = false;
                    lock (gate)
                    {
                        teardownCompleted = true;
                        publishCompleted = TryMarkDisposalsCompleted();
                    }

                    if (publishCompleted)
                        PublishDisposalsCompleted();
                }

                void TryDisposeItem(TObject item)
                {
                    if (item is IDisposable disposable)
                        disposable.Dispose();
                    else if (item is IAsyncDisposable asyncDisposable)
                        TrackAsyncDisposal(asyncDisposable.DisposeAsync().AsTask());
                }

                void TrackAsyncDisposal(Task disposalTask)
                {
                    lock (gate)
                    {
                        ++pendingAsyncDisposals;
                    }

                    if (disposalTask.IsCompleted)
                    {
                        CompleteAsyncDisposal(disposalTask);
                        return;
                    }

                    _ = disposalTask.ContinueWith(
                        static (completedTask, state) => ((Action<Task>)state!).Invoke(completedTask),
                        (Action<Task>)CompleteAsyncDisposal,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }

                void CompleteAsyncDisposal(Task disposalTask)
                {
                    if (disposalTask.IsFaulted)
                    {
                        TryFailDisposals(disposalTask.Exception.InnerExceptions.Count == 1
                            ? disposalTask.Exception.InnerException!
                            : disposalTask.Exception);
                    }
                    else if (disposalTask.IsCanceled)
                    {
                        TryFailDisposals(new TaskCanceledException(disposalTask));
                    }

                    var publishCompleted = false;
                    lock (gate)
                    {
                        --pendingAsyncDisposals;
                        publishCompleted = TryMarkDisposalsCompleted();
                    }

                    if (publishCompleted)
                        PublishDisposalsCompleted();
                }

                bool TryMarkDisposalsCompleted()
                {
                    if (!teardownCompleted || pendingAsyncDisposals != 0 || disposalsFinalized)
                        return false;

                    disposalsFinalized = true;
                    return true;
                }

                void PublishDisposalsCompleted()
                {
                    disposalsCompleted.OnNext(Unit.Default);
                    disposalsCompleted.OnCompleted();
                }

                void TryFailDisposals(Exception error)
                {
                    var publishError = false;
                    lock (gate)
                    {
                        if (!disposalsFinalized)
                        {
                            disposalsFinalized = true;
                            publishError = true;
                        }
                    }

                    if (publishError)
                        disposalsCompleted.OnError(error);
                }
            });
    }
}
#endif
