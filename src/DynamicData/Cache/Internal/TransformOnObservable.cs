// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Create<IChangeSet<TDestination, TKey>>(observer =>
    {
        var shutdownSubject = new Subject<TKey>();
        var changeEmitter = new ChangeEmiter(observer);
        var locker = InternalEx.NewLock();
        var compositeDisposable = new CompositeDisposable(shutdownSubject);

        // Create the sub-observable that takes the result of the transformation,
        // filters out unchanged values, and then updates the cache
        void CreateChildSubscription(TSource obj, TKey key)
        {
            IDisposable? disposable = null;
            var completed = false;

            // Add a new subscription
            changeEmitter.AddSubscription();

            // Create the subscription
            disposable = transform(obj, key)
                .DistinctUntilChanged()
                .TakeUntil(shutdownSubject.Where(shutdownKey => EqualityComparer<TKey>.Default.Equals(key, shutdownKey)))
                .Synchronize(locker!)
                .Subscribe(
                    val =>
                    {
                        changeEmitter.Cache.AddOrUpdate(val, key);
                        changeEmitter.EmitChanges(fromSource: false);
                    },
                    () =>
                    {
                        if (disposable is not null)
                        {
                            compositeDisposable.Remove(disposable);
                        }
                        changeEmitter.OnCompleted();
                        completed = true;
                    });

            // If not already completed, add it to the CompositeDisposable
            if (!completed)
            {
                compositeDisposable.Add(disposable);
            }
        }

        // Create a subscription to the source that processes the changes inside the lock
        var subscription = source
            .Synchronize(locker!)
            .Subscribe(
                changes =>
                {
                    // Flag a parent update is happening once inside the lock
                    changeEmitter.MarkSourceUpdate();

                    // Process all the changes at once to preserve the changeset order
                    foreach (var change in changes.ToConcreteType())
                    {
                        switch (change.Reason)
                        {
                            // Create a subscription that will update the cache
                            case ChangeReason.Add:
                                CreateChildSubscription(change.Current, change.Key);
                                break;

                            // Shutdown the existing subscription and remove from the cache
                            case ChangeReason.Remove:
                                shutdownSubject.OnNext(change.Key);
                                changeEmitter.Cache.Remove(change.Key);
                                break;

                            // Shutdown the existing subscription and create a new one
                            case ChangeReason.Update:
                                shutdownSubject.OnNext(change.Key);
                                CreateChildSubscription(change.Current, change.Key);
                                break;

                            // Let the downstream decide what this means
                            case ChangeReason.Refresh:
                                changeEmitter.Cache.Refresh(change.Key);
                                break;
                        }
                    }

                    // Emit all of the changes
                    changeEmitter.EmitChanges(fromSource: true);
                },
                observer.OnError,
                changeEmitter.OnCompleted);

        // Add the source subscription to the clean up list
        compositeDisposable.Add(subscription);

        // Return the single disposable that controls everything
        return compositeDisposable;
    });

    private class ChangeEmiter(IObserver<IChangeSet<TDestination, TKey>> observer)
    {
        private bool _sourceUpdate;
        private int _subscriptionCounter = 1;

        public ChangeAwareCache<TDestination, TKey> Cache { get; } = new();

        public void MarkSourceUpdate() => _sourceUpdate = true;

        public void EmitChanges(bool fromSource)
        {
            if (fromSource || !_sourceUpdate)
            {
                var changes = Cache.CaptureChanges();
                if (changes.Count > 0)
                {
                    observer.OnNext(changes);
                }

                _sourceUpdate = false;
            }
        }

        public void AddSubscription() => Interlocked.Increment(ref _subscriptionCounter);

        public void OnCompleted()
        {
            if (Interlocked.Decrement(ref _subscriptionCounter) == 0)
            {
                observer.OnCompleted();
            }
        }
    }
}
