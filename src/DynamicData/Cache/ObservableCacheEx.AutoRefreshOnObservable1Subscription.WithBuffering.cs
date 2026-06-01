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

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData;

public static partial class ObservableCacheEx
{
    private static partial class AutoRefreshOnObservable1Subscription
    {
        public sealed class WithBuffering<TObject, TKey, TAny>
                : Base<TObject, TKey, TAny>
            where TObject : notnull
            where TKey : notnull
        {
            public static WithBuffering<TObject, TKey, TAny> CreateAndActivate(
                IObserver<IChangeSet<TObject, TKey>>    downstreamObserver,
                TimeSpan                                refreshBufferWindow,
                Func<TObject, TKey, IObservable<TAny>>  refreshRequestedFactory,
                IScheduler?                             scheduler,
                IObservable<IChangeSet<TObject, TKey>>  source)
            {
                var subscription = new WithBuffering<TObject, TKey, TAny>(
                    downstreamObserver:         downstreamObserver,
                    refreshRequestedFactory:    refreshRequestedFactory);
                    
                subscription.Activate(
                    refreshBufferWindow:    refreshBufferWindow,
                    scheduler:              scheduler,
                    source:                 source);
                
                return subscription;
            }
        
            private WithBuffering(
                    IObserver<IChangeSet<TObject, TKey>>    downstreamObserver,
                    Func<TObject, TKey, IObservable<TAny>>  refreshRequestedFactory)
                : base(
                    downstreamObserver:         downstreamObserver,
                    refreshRequestedFactory:    refreshRequestedFactory)
            {
                _onRefreshRequestReceived   = new();
                _requestedRefreshKeysBuffer = new();
            }

            protected override void OnDisposing(bool includeManagedResources)
            {
                if (!includeManagedResources)
                    return;
            
                base.OnDisposing(includeManagedResources);
                
                _refreshBufferClosedSubscription?.Dispose();                
            }

            protected override void OnRefreshRequestReceived(Change<TObject, TKey> refreshChange)
                => _onRefreshRequestReceived.OnNext(refreshChange);

            private void Activate(
                TimeSpan                                refreshBufferWindow,
                IScheduler?                             scheduler,
                IObservable<IChangeSet<TObject, TKey>>  source)
            {
                _refreshBufferClosedSubscription = _onRefreshRequestReceived
                    .Buffer(
                        timeSpan:   refreshBufferWindow,
                        scheduler:  scheduler ?? GlobalConfig.DefaultScheduler)
                    .SynchronizeSafe(NotificationQueue)
                    .SubscribeSafe(OnRefreshBufferClosedNext);
                    
                Activate(source);
            }

            private void OnRefreshBufferClosedNext(IList<Change<TObject, TKey>> refreshChanges)
            {
                if (refreshChanges.Count is 0)
                    return;
                        
                var changeSet = new ChangeSet<TObject, TKey>(capacity: refreshChanges.Count);
                            
                foreach (var refreshChange in refreshChanges)
                {
                    // De-duplicate repeated refreshes for the same item.
                    // Also make sure the item wasn't removed during the buffer window, before we publish
                    // a refresh for it.  
                    if (        _requestedRefreshKeysBuffer.Add(refreshChange.Key)
                                &&  RefreshRequestedSubscriptionsContainsKey(refreshChange.Key))
                        changeSet.Add(refreshChange);
                }
                _requestedRefreshKeysBuffer.Clear();
                            
                if (changeSet.Count is not 0)
                    DownstreamObserver.OnNext(changeSet);
            }

            private readonly Subject<Change<TObject, TKey>> _onRefreshRequestReceived;
            private readonly HashSet<TKey>                  _requestedRefreshKeysBuffer;

            private IDisposable? _refreshBufferClosedSubscription;
        }
    }
}
