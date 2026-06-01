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
#pragma warning disable SA1124
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
    private abstract class SubscribeManySubscriptionBase<TObject, TKey, TChild, TResult>
            : IDisposable
        where TObject : notnull
        where TKey : notnull
    {
        protected SubscribeManySubscriptionBase(IObserver<TResult> downstreamObserver)
        {
            _downstreamObserver = downstreamObserver;

            _notificationQueue                      = new();
            _childSourceSubscriptionContainersByKey = new();
        }
        
        ~SubscribeManySubscriptionBase()
            => OnDisposing(includeManagedResources: false);
        
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _hasDisposed, 1) is not 0)
                return;

            _sourceSubscription?.Dispose();

            foreach (var subscription in _childSourceSubscriptionContainersByKey.Values)
                subscription.Dispose();

            _notificationQueue.Dispose();

            OnDisposing(includeManagedResources: true);
            
            GC.SuppressFinalize(this);
        }

        protected IObserver<TResult> DownstreamObserver
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

        protected abstract void AfterSourceNext(IChangeSet<TObject, TKey> upstreamChanges);
        
        protected abstract IObservable<TChild> BuildChildSource(
            TObject item,
            TKey    key);
        
        protected bool ContainsSourceItemKey(TKey key)
            => _childSourceSubscriptionContainersByKey.ContainsKey(key);
        
        protected abstract void OnChildSourceNext(
            TObject item,
            TKey    key);
            
        protected virtual void OnDisposing(bool includeManagedResources) { }
        
        protected void OnSourceItemAdded(
            TObject item,
            TKey    key)
        {
            var subscriptionContainer = new SingleAssignmentDisposable();

            _childSourceSubscriptionContainersByKey.Add(
                key:    key,
                value:  subscriptionContainer);

            BuildChildSourceSubscription(
                container:  subscriptionContainer,
                item:       item,
                key:        key);
        }

        protected void OnSourceItemRemoved(TKey key)
        {
            DisposeChildSourceSubscription(_childSourceSubscriptionContainersByKey[key]);

            _childSourceSubscriptionContainersByKey.Remove(key);
        }
        
        protected void OnSourceItemReplaced(
            TObject newItem,
            TKey    key)
        {
            DisposeChildSourceSubscription(_childSourceSubscriptionContainersByKey[key]);
                                
            var subscriptionContainer = new SingleAssignmentDisposable();
            _childSourceSubscriptionContainersByKey[key] = subscriptionContainer;

            BuildChildSourceSubscription(
                container:  subscriptionContainer,
                item:       newItem,
                key:        key);
        }
        
        protected abstract void ProcessSourceChange(Change<TObject, TKey> change);

        private void BuildChildSourceSubscription(
            SingleAssignmentDisposable  container,
            TObject                     item,
            TKey                        key)
        {
            IDisposable subscription;
            try
            {
                subscription = BuildChildSource(item, key)
                    .SynchronizeSafe(_notificationQueue)
                    .SubscribeSafe(
                        onNext:         _ => OnChildSourceNext(item, key),
                        onError:        OnError,
                        onCompleted:    () => OnChildSourceCompleted(key));
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
        
        private void DisposeChildSourceSubscription(SingleAssignmentDisposable container)
        {
            if (container.IsDisposed)
                --_childSourceCompletionCount;
            else
                container.Dispose();
        }
        
        private void OnError(Exception error)
        {
            _downstreamObserver.OnError(error);

            foreach (var container in _childSourceSubscriptionContainersByKey.Values)
                container.Dispose();

            _hasFailed = true;
        }

        private void OnChildSourceCompleted(TKey key)
        {
            ++_childSourceCompletionCount;

            // Disposing a completed subscription obviously isn't necessary, as far as RX is concerned, but it allows us
            // to use .IsDisposed to keep track of _refreshRequestedCompletionCount, elsewhere.
            _childSourceSubscriptionContainersByKey[key].Dispose();
            
            if (        _hasSourceCompleted
                    &&  (_childSourceCompletionCount == _childSourceSubscriptionContainersByKey.Count))
                _downstreamObserver.OnCompleted();
        }
        
        private void OnSourceCompleted()
        {
            _hasSourceCompleted = true;

            if (_childSourceCompletionCount == _childSourceSubscriptionContainersByKey.Count)
                _downstreamObserver.OnCompleted();
        }

        private void OnSourceNext(IChangeSet<TObject, TKey> upstreamChanges)
        {
            foreach (var change in upstreamChanges)
            {
                // Don't continue creating new subscriptions after an error occurs.
                if (_hasFailed)
                    return;
            
                ProcessSourceChange(change);
            }
            
            AfterSourceNext(upstreamChanges);
        }

        private readonly Dictionary<TKey, SingleAssignmentDisposable>   _childSourceSubscriptionContainersByKey;
        private readonly IObserver<TResult>                             _downstreamObserver;
        private readonly SharedDeliveryQueue                            _notificationQueue;
        
        private int             _hasDisposed;
        private bool            _hasFailed;
        private bool            _hasSourceCompleted;
        private int             _childSourceCompletionCount;
        private IDisposable?    _sourceSubscription;
    }
}
