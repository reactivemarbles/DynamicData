// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Create<IChangeSet<TDestination, TKey>>(observer => new Subscription(source, transform, observer));

    private sealed class Subscription : IDisposable
    {
#if NET9_0_OR_GREATER
        private readonly Lock _synchronize = new();
#else
        private readonly object _synchronize = new();
#endif
        private readonly ChangeAwareCache<TDestination, TKey> _cache = new();
        private readonly CompositeDisposable _compositeDisposable = [];
        private readonly Func<TSource, TKey, IObservable<TDestination>> _transform;
        private readonly IDisposable _sourceSubscription;
        private readonly IObserver<IChangeSet<TDestination, TKey>> _observer;
        private readonly Dictionary<TKey, IDisposable> _keySubscriptions = new();
        private int _subscriptionCounter = 1;
        private bool _sourceUpdate;

        public Subscription(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, IObserver<IChangeSet<TDestination, TKey>> observer)
        {
            _observer = observer;
            _transform = transform;
            _sourceSubscription = source
                .Synchronize(_synchronize)
                .SubscribeSafe(Observer.Create<IChangeSet<TSource, TKey>>(ProcessChangeSet, observer.OnError, OnCompleted));
        }

        public void Dispose()
        {
            _sourceSubscription.Dispose();
            _compositeDisposable.Dispose();
            _keySubscriptions.Values.ForEach(sub => sub.Dispose());
        }

        private void ProcessChangeSet(IChangeSet<TSource, TKey> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }

            // Flag a source update is happening
            _sourceUpdate = true;

            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Create a subscription that will update the cache
                    case ChangeReason.Add:
                        CreateTransformSubscription(change.Current, change.Key);
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        RemoveKey(change.Key);
                        _cache.Remove(change.Key);
                        break;

                    // Shutdown the existing subscription and create a new one
                    case ChangeReason.Update:
                        RemoveKey(change.Key);
                        CreateTransformSubscription(change.Current, change.Key);
                        break;

                    // Let the downstream decide what this means
                    case ChangeReason.Refresh:
                        _cache.Refresh(change.Key);
                        break;
                }
            }

            // Emit all of the changes
            EmitChanges(fromSource: true);
        }

        private void RemoveKey(TKey key)
        {
            if (_keySubscriptions.TryGetValue(key, out var disposable))
            {
                disposable.Dispose();
                _keySubscriptions.Remove(key);
            }
        }

        private void EmitChanges(bool fromSource)
        {
            if (fromSource || !_sourceUpdate)
            {
                var changes = _cache.CaptureChanges();
                if (changes.Count > 0)
                {
                    _observer.OnNext(changes);
                }

                _sourceUpdate = false;
            }
        }

        private void OnCompleted()
        {
            if (Interlocked.Decrement(ref _subscriptionCounter) == 0)
            {
                _observer.OnCompleted();
            }
        }

        // Create the sub-observable that takes the result of the transformation,
        // filters out unchanged values, and then updates the cache
        private void CreateTransformSubscription(TSource obj, TKey key)
        {
            // Add a new subscription
            Interlocked.Increment(ref _subscriptionCounter);

            // Create the transformation observable for the source item
            // Filter out unchanged values
            // And update the cache with the latest value
            var disposable = _transform(obj, key)
                .DistinctUntilChanged()
                .Synchronize(_synchronize)
                .SubscribeSafe(Observer.Create<TDestination>(
                    val =>
                    {
                        _cache.AddOrUpdate(val, key);
                        EmitChanges(fromSource: false);
                    },
                    _observer.OnError,
                    OnCompleted));

            // Add it to the Dictionary
            _keySubscriptions.Add(key, disposable);
        }
    }
}
