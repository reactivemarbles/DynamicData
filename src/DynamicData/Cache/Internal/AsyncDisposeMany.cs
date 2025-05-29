// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Internal;

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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        disposalsCompletedAccessor.ThrowArgumentNullExceptionIfNull(nameof(disposalsCompletedAccessor));

        return Observable
            .Create<IChangeSet<TObject, TKey>>(downstreamObserver =>
            {
                var itemsByKey = new Dictionary<TKey, TObject>();

                var synchronizationGate = InternalEx.NewLock();

                var disposals = new Subject<IObservable<Unit>>();
                var disposalsCompleted = disposals
                    .Merge()
                    .IgnoreElements()
                    .Concat(Observable.Return(Unit.Default))
                    // If no one subscribes to this stream, disposals won't actually occur, so make sure we have one (and only one) regardless of what the consumer does.
                    .Publish()
                    .AutoConnect(0);

                // Make sure the consumer gets a chance to subscribe BEFORE we actually start processing items, so there's no risk of the consumer missing notifications.
                disposalsCompletedAccessor.Invoke(disposalsCompleted);

                var sourceSubscription = source
                    .Synchronize(synchronizationGate)
                    // Using custom notification handlers instead of .Do() to make sure that we're not disposing items until AFTER we've notified all downstream listeners to remove them from their cached or bound collections.
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
                    lock (synchronizationGate)
                    {
                        sourceSubscription.Dispose();

                        TearDown();
                    }
                });

                void TearDown()
                {
                    if (disposals.HasObservers)
                    {
                        try
                        {
                            foreach (var item in itemsByKey.Values)
                                TryDisposeItem(item);
                            disposals.OnCompleted();

                            itemsByKey.Clear();
                        }
                        catch (Exception error)
                        {
                            disposals.OnError(error);
                        }
                    }
                }

                void TryDisposeItem(TObject item)
                {
                    if (item is IDisposable disposable)
                        disposable.Dispose();
                    else if (item is IAsyncDisposable asyncDisposable)
                        disposals.OnNext(Observable.FromAsync(() => asyncDisposable.DisposeAsync().AsTask()));
                }
            });
    }
}
#endif
