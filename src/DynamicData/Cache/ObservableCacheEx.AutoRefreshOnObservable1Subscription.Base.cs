// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#pragma warning disable CA1822
#pragma warning disable RCS1001
#pragma warning disable RCS1037
#pragma warning disable SA1008
#pragma warning disable SA1019
#pragma warning disable SA1025
#pragma warning disable SA1028
#pragma warning disable SA1502
#pragma warning disable SA1505
#pragma warning disable SA1507
#pragma warning disable SA1508
#pragma warning disable SA1519
#pragma warning disable SA1600

using System.Reactive.Disposables;

namespace DynamicData;

public static partial class ObservableCacheEx
{
    private static partial class AutoRefreshOnObservable1Subscription
    {
        public abstract class Base<TObject, TKey, TAny>
                : IDisposable
            where TObject : notnull
            where TKey : notnull
        {
            protected Base(
                IObserver<IChangeSet<TObject, TKey>>    downstreamObserver,
                Func<TObject, TKey, IObservable<TAny>>  refreshRequestedFactory)
            {
                _downstreamObserver         = downstreamObserver;
                _refreshRequestedFactory    = refreshRequestedFactory;

                _notificationQueue                              = new();
                _refreshRequestedSubscriptionContainersByKey    = new();
            }
            
            ~Base()
                => OnDisposing(includeManagedResources: false);
            
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _hasDisposed, 1) is not 0)
                    return;

                _sourceSubscription?.Dispose();

                foreach (var subscription in _refreshRequestedSubscriptionContainersByKey.Values)
                    subscription.Dispose();

                _notificationQueue.Dispose();
                
                OnDisposing(includeManagedResources: true);
                
                GC.SuppressFinalize(this);
            }

            protected IObserver<IChangeSet<TObject, TKey>> DownstreamObserver
                => _downstreamObserver;
                
            protected SharedDeliveryQueue NotificationQueue
                => _notificationQueue;

            protected void Activate(IObservable<IChangeSet<TObject, TKey>> source)
                => _sourceSubscription = source
                    .SynchronizeSafe(_notificationQueue)
                    .SubscribeSafe(
                        onNext:         OnSourceNext,
                        onError:        OnError,
                        onCompleted:    OnSourceCompleted);
            
            protected virtual void OnDisposing(bool includeManagedResources) { }

            protected abstract void OnRefreshRequestReceived(Change<TObject, TKey> refreshChange);
            
            protected bool RefreshRequestedSubscriptionsContainsKey(TKey key)
                => _refreshRequestedSubscriptionContainersByKey.ContainsKey(key);
            
            private void BuildRefreshRequestedSubscription(
                SingleAssignmentDisposable  container,
                TObject                     item,
                TKey                        key)
            {
                IDisposable subscription;
                try
                {
                    subscription = _refreshRequestedFactory.Invoke(item, key)
                        .SynchronizeSafe(_notificationQueue)
                        .SubscribeSafe(
                            onNext:         _ => OnRefreshRequestedNext(item, key),
                            onError:        OnError,
                            onCompleted:    () => OnRefreshRequestedCompleted(key));
                }
                catch (Exception error)
                {
                    OnError(error);
                    return;
                }

                // Subscriptions are stored in a container so that they can be added to the collection that tracks them,
                // before they exist. This allows us to do 2 things:
                //  A) Use .IsDisposed to track which subscriptions have completed and which haven't.
                //  B) Accomodate subscriptions that complete immediately, during creation.
                // Don't assign the new subscription to the container if it completed immediately, otherwise it'll reset
                // the container's .IsDisposed flag. 
                if (!container.IsDisposed)
                    container.Disposable = subscription;
            }
            
            private void DisposeRefreshRequestedSubscription(SingleAssignmentDisposable container)
            {
                if (container.IsDisposed)
                    --_refreshRequestedCompletionCount;
                else
                    container.Dispose();
            }
            
            private void OnError(Exception error)
            {
                _downstreamObserver.OnError(error);

                foreach (var subscription in _refreshRequestedSubscriptionContainersByKey.Values)
                    subscription.Dispose();

                _hasFailed = true;
            }

            private void OnRefreshRequestedCompleted(TKey key)
            {
                ++_refreshRequestedCompletionCount;

                // Disposing a completed subscription obviously isn't necessary, as far as RX is concerned, but it allows us
                // to use .IsDisposed to keep track of _refreshRequestedCompletionCount, elsewhere.
                _refreshRequestedSubscriptionContainersByKey[key].Dispose();
                
                if (        _hasSourceCompleted
                        &&  (_refreshRequestedCompletionCount == _refreshRequestedSubscriptionContainersByKey.Count))
                    _downstreamObserver.OnCompleted();
            }
            
            private void OnRefreshRequestedNext(
                    TObject item,
                    TKey    key)
                => OnRefreshRequestReceived(new(
                    reason:     ChangeReason.Refresh,
                    key:        key,
                    current:    item));
            
            private void OnSourceCompleted()
            {
                _hasSourceCompleted = true;

                if (_refreshRequestedCompletionCount == _refreshRequestedSubscriptionContainersByKey.Count)
                    _downstreamObserver.OnCompleted();
            }

            private void OnSourceNext(IChangeSet<TObject, TKey> upstreamChanges)
            {
                foreach (var change in upstreamChanges)
                {
                    // Don't continue creating new subscriptions after an error occurs.
                    if (_hasFailed)
                        return;
                
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                var subscriptionContainer = new SingleAssignmentDisposable();

                                _refreshRequestedSubscriptionContainersByKey.Add(
                                    key:    change.Key,
                                    value:  subscriptionContainer);

                                BuildRefreshRequestedSubscription(
                                    container:  subscriptionContainer,
                                    item:       change.Current,
                                    key:        change.Key);
                            }
                            break;

                        case ChangeReason.Remove:
                            {
                                DisposeRefreshRequestedSubscription(_refreshRequestedSubscriptionContainersByKey[change.Key]);

                                _refreshRequestedSubscriptionContainersByKey.Remove(change.Key);
                            }
                            break;

                        case ChangeReason.Update:
                            {
                                DisposeRefreshRequestedSubscription(_refreshRequestedSubscriptionContainersByKey[change.Key]);
                                    
                                var subscriptionContainer = new SingleAssignmentDisposable();
                                _refreshRequestedSubscriptionContainersByKey[change.Key] = subscriptionContainer;

                                BuildRefreshRequestedSubscription(
                                    container:  subscriptionContainer,
                                    item:       change.Current,
                                    key:        change.Key);
                            }
                            break;
                    }
                }
                
                _downstreamObserver.OnNext(upstreamChanges);
            }

            private readonly IObserver<IChangeSet<TObject, TKey>>           _downstreamObserver;
            private readonly SharedDeliveryQueue                            _notificationQueue;
            private readonly Func<TObject, TKey, IObservable<TAny>>         _refreshRequestedFactory;
            private readonly Dictionary<TKey, SingleAssignmentDisposable>   _refreshRequestedSubscriptionContainersByKey;
            
            private int             _hasDisposed;
            private bool            _hasFailed;
            private bool            _hasSourceCompleted;
            private int             _refreshRequestedCompletionCount;
            private IDisposable?    _sourceSubscription;
        }
    }
}
