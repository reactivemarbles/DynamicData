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

namespace DynamicData;

public static partial class ObservableCacheEx
{
    private static partial class AutoRefreshOnObservable1Subscription
    {
        public sealed class WithoutBuffering<TObject, TKey, TAny>
                : Base<TObject, TKey, TAny>
            where TObject : notnull
            where TKey : notnull
        {
            public static WithoutBuffering<TObject, TKey, TAny> CreateAndActivate(
                IObserver<IChangeSet<TObject, TKey>>    downstreamObserver,
                Func<TObject, TKey, IObservable<TAny>>  refreshRequestedFactory,
                IObservable<IChangeSet<TObject, TKey>>  source)
            {
                var subscription = new WithoutBuffering<TObject, TKey, TAny>(
                    downstreamObserver:         downstreamObserver,
                    refreshRequestedFactory:    refreshRequestedFactory);
                    
                subscription.Activate(source);
                
                return subscription;
            }
        
            private WithoutBuffering(
                    IObserver<IChangeSet<TObject, TKey>>    downstreamObserver,
                    Func<TObject, TKey, IObservable<TAny>>  refreshRequestedFactory)
                : base(
                    downstreamObserver:         downstreamObserver,
                    refreshRequestedFactory:    refreshRequestedFactory)
            { }

            protected override void OnRefreshRequestReceived(Change<TObject, TKey> refreshChange)
                => DownstreamObserver.OnNext(new ChangeSet<TObject, TKey>() { refreshChange });
        }
    }
}
