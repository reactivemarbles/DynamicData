// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the Switch class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="sources">The sources value.</param>
internal sealed class Switch<TObject, TKey>(IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _sources field.
    /// </summary>
    private readonly IObservable<IObservable<IChangeSet<TObject, TKey>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                // Switching is done by hand rather than with Observable.Switch, which holds its gate for
                // the whole of downstream delivery. The queue enqueues and returns instead, so a producer
                // is never held up by whatever a subscriber does with the notification, and a pipeline
                // crossing into another collection cannot deadlock against it.
                var queue = new DeliveryQueue<IChangeSet<TObject, TKey>>(observer);

                // What the current source has contributed, so that switching away can take it back out.
                var current = new ChangeAwareCache<TObject, TKey>();
                var subscription = new SerialDisposable();

                // Identifies the current source. A superseded one may still be mid-delivery, and anything
                // it produces after this point belongs to a source that has already been switched away from.
                var active = 0;
                var isSourceRunning = false;
                var areSourcesComplete = false;

                var outer = _sources.SubscribeSafe(Observer.Create<IObservable<IChangeSet<TObject, TKey>>>(
                    source =>
                    {
                        int id;

                        using (var scope = queue.AcquireLock())
                        {
                            id = ++active;
                            isSourceRunning = true;

                            if (current.Count != 0)
                            {
                                current.Clear();
                                var clears = current.CaptureChanges();

                                if (clears.Count != 0)
                                {
                                    scope.EnqueueNext(clears);
                                }
                            }
                        }

                        // Subscribed outside the lock. The source may deliver synchronously, and that
                        // delivery takes the lock for itself.
                        if (id != Volatile.Read(ref active))
                        {
                            return;
                        }

                        var innerSubscription = new SingleAssignmentDisposable();
                        subscription.Disposable = innerSubscription;
                        innerSubscription.Disposable = source.SubscribeSafe(Observer.Create<IChangeSet<TObject, TKey>>(
                            changes =>
                            {
                                using var scope = queue.AcquireLock();

                                if (id != active)
                                {
                                    return;
                                }

                                current.Clone(changes);
                                var capturedChanges = current.CaptureChanges();

                                if (capturedChanges.Count != 0)
                                {
                                    scope.EnqueueNext(capturedChanges);
                                }
                            },
                            error =>
                            {
                                using var scope = queue.AcquireLock();

                                if (id != active)
                                {
                                    return;
                                }

                                scope.EnqueueError(error);
                            },
                            () =>
                            {
                                using var scope = queue.AcquireLock();

                                if (id != active)
                                {
                                    return;
                                }

                                isSourceRunning = false;

                                if (areSourcesComplete)
                                {
                                    scope.EnqueueCompleted();
                                }
                            }));
                    },
                    queue.OnError,
                    () =>
                    {
                        using var scope = queue.AcquireLock();

                        areSourcesComplete = true;

                        // The current source may still be running, and the result ends only once both have.
                        if (!isSourceRunning)
                        {
                            scope.EnqueueCompleted();
                        }
                    }));

                // Disposal order matters and CompositeDisposable does not specify one. The queue goes first
                // so that any delivery in flight is finished before the subscriptions feeding it are torn down.
                return Disposable.Create(() =>
                {
                    queue.Dispose();
                    outer.Dispose();
                    subscription.Dispose();
                });
            });
}
