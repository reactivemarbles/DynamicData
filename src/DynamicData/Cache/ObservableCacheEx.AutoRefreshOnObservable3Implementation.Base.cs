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

namespace DynamicData;

public static partial class ObservableCacheEx
{
    private static partial class AutoRefreshOnObservable3Implementation
    {
        public abstract class Base<TObject, TKey, TAny>
                : ISubscribeManyImplementation<TObject, TKey, TAny>
            where TObject : notnull
            where TKey : notnull
        {
            protected Base(
                ISubscribeManyOrchestrator<TObject, TKey, IChangeSet<TObject, TKey>>    orchestrator,
                Func<TObject, TKey, IObservable<TAny>>                                  refreshRequestedFactory)
            {
                _orchestrator               = orchestrator;
                _refreshRequestedFactory    = refreshRequestedFactory;
            }
        
            public void AfterSourceNext(IChangeSet<TObject, TKey> upstreamChanges)
                => _orchestrator.DownstreamObserver.OnNext(upstreamChanges);
            
            public IObservable<TAny> BuildChildSource(
                    TObject item,
                    TKey    key)
                => _refreshRequestedFactory.Invoke(item, key);
                
            public void OnChildSourceNext(
                    TObject item,
                    TKey    key)
                => OnRefreshRequestReceived(new(
                    reason:     ChangeReason.Refresh,
                    key:        key,
                    current:    item));
                
            public void ProcessSourceChange(Change<TObject, TKey> change)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        _orchestrator.OnSourceItemAdded(
                            item:   change.Current,
                            key:    change.Key);
                        break;

                    case ChangeReason.Remove:
                        _orchestrator.OnSourceItemRemoved(change.Key);
                        break;

                    case ChangeReason.Update:
                        _orchestrator.OnSourceItemReplaced(
                            newItem:    change.Current,
                            key:        change.Key);
                        break;
                }
            }

            protected ISubscribeManyOrchestrator<TObject, TKey, IChangeSet<TObject, TKey>> Orchestrator
                => _orchestrator;

            protected abstract void OnRefreshRequestReceived(Change<TObject, TKey> refreshChange);

            private readonly ISubscribeManyOrchestrator<TObject, TKey, IChangeSet<TObject, TKey>>   _orchestrator;
            private readonly Func<TObject, TKey, IObservable<TAny>>                                 _refreshRequestedFactory;
        }
    }
}
