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
        public sealed class WithoutBuffering<TObject, TKey, TAny>
                : Base<TObject, TKey, TAny>
            where TObject : notnull
            where TKey : notnull
        {
            public WithoutBuffering(
                    ISubscribeManyOrchestrator<TObject, TKey, IChangeSet<TObject, TKey>>    orchestrator,
                    Func<TObject, TKey, IObservable<TAny>>                                  refreshRequestedFactory)
                : base(
                    orchestrator:               orchestrator,
                    refreshRequestedFactory:    refreshRequestedFactory)
            { }
        
            protected override void OnRefreshRequestReceived(Change<TObject, TKey> refreshChange)
                => Orchestrator.DownstreamObserver.OnNext(new ChangeSet<TObject, TKey>() { refreshChange });
        }
    }
}
