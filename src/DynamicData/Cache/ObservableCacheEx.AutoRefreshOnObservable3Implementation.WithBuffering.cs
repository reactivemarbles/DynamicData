// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

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

namespace DynamicData;

public static partial class ObservableCacheEx
{
    private static partial class AutoRefreshOnObservable3Implementation
    {
        public sealed class WithBuffering<TObject, TKey, TAny>
                : Base<TObject, TKey, TAny>,
                    IDisposable
            where TObject : notnull
            where TKey : notnull
        {
            public static WithBuffering<TObject, TKey, TAny> CreateAndActivate(
                ISubscribeManyOrchestrator<TObject, TKey, IChangeSet<TObject, TKey>>    orchestrator,
                TimeSpan                                                                refreshBufferWindow,
                Func<TObject, TKey, IObservable<TAny>>                                  refreshRequestedFactory,
                IScheduler?                                                             scheduler)
            {
                var implementation = new WithBuffering<TObject, TKey, TAny>(
                    orchestrator:               orchestrator,
                    refreshRequestedFactory:    refreshRequestedFactory);
                
                implementation.Activate(
                    refreshBufferWindow:    refreshBufferWindow,
                    scheduler:              scheduler);

                return implementation;
            }
        
            private WithBuffering(
                    ISubscribeManyOrchestrator<TObject, TKey, IChangeSet<TObject, TKey>>    orchestrator,
                    Func<TObject, TKey, IObservable<TAny>>                                  refreshRequestedFactory)
                : base(
                    orchestrator:               orchestrator,
                    refreshRequestedFactory:    refreshRequestedFactory)
            {
                _onRefreshRequestReceived   = new();
                _requestedRefreshKeysBuffer = new();
            }
        
            public void Dispose()
            {
                _refreshBufferClosedSubscription?.Dispose();

                _onRefreshRequestReceived.Dispose();
            }

            protected override void OnRefreshRequestReceived(Change<TObject, TKey> refreshChange)
                => _onRefreshRequestReceived.OnNext(refreshChange);

            private void Activate(
                    TimeSpan    refreshBufferWindow,
                    IScheduler? scheduler)
                => _refreshBufferClosedSubscription = _onRefreshRequestReceived
                    .Buffer(
                        timeSpan:   refreshBufferWindow,
                        scheduler:  scheduler ?? GlobalConfig.DefaultScheduler)
                    .SynchronizeSafe(Orchestrator.NotificationQueue)
                    .SubscribeSafe(OnRefreshBufferClosedNext);

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
                                &&  Orchestrator.ContainsSourceItemKey(refreshChange.Key))
                        changeSet.Add(refreshChange);
                }
                _requestedRefreshKeysBuffer.Clear();
                                
                if (changeSet.Count is not 0)
                    Orchestrator.DownstreamObserver.OnNext(changeSet);
            }

            private readonly Subject<Change<TObject, TKey>>         _onRefreshRequestReceived;
            private readonly HashSet<TKey>                          _requestedRefreshKeysBuffer;
            
            private IDisposable? _refreshBufferClosedSubscription;
        }
    }
}
