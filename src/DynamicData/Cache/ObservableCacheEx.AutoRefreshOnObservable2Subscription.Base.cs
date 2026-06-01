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
    private static partial class AutoRefreshOnObservable2Subscription
    {
        public abstract class Base<TObject, TKey, TAny>
                : SubscribeManySubscriptionBase<TObject, TKey, TAny, IChangeSet<TObject, TKey>>
            where TObject : notnull
            where TKey : notnull
        {
            protected Base(
                    IObserver<IChangeSet<TObject, TKey>>    downstreamObserver,
                    Func<TObject, TKey, IObservable<TAny>>  refreshRequestedFactory)
                : base(downstreamObserver: downstreamObserver)
            {
                _refreshRequestedFactory = refreshRequestedFactory;
            }

            protected override void AfterSourceNext(IChangeSet<TObject, TKey> upstreamChanges)
                => DownstreamObserver.OnNext(upstreamChanges);

            protected override IObservable<TAny> BuildChildSource(
                    TObject item,
                    TKey    key)
                => _refreshRequestedFactory.Invoke(item, key);

            protected override void OnChildSourceNext(
                    TObject item,
                    TKey    key)
                => OnRefreshRequestReceived(new(
                    reason:     ChangeReason.Refresh,
                    key:        key,
                    current:    item));

            protected abstract void OnRefreshRequestReceived(Change<TObject, TKey> refreshChange);

            protected override void ProcessSourceChange(Change<TObject, TKey> change)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        OnSourceItemAdded(
                            item:   change.Current,
                            key:    change.Key);
                        break;

                    case ChangeReason.Remove:
                        OnSourceItemRemoved(change.Key);
                        break;

                    case ChangeReason.Update:
                        OnSourceItemReplaced(
                            newItem:    change.Current,
                            key:        change.Key);
                        break;
                }
            }

            private readonly Func<TObject, TKey, IObservable<TAny>> _refreshRequestedFactory;
        }
    }
}
