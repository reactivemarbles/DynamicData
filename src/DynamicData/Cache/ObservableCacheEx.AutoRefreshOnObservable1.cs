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
#pragma warning disable SA1505
#pragma warning disable SA1507
#pragma warning disable SA1508
#pragma warning disable SA1519
#pragma warning disable SA1600

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData;

public static partial class ObservableCacheEx
{
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable1<TObject, TKey, TAny>(
            this    IObservable<IChangeSet<TObject, TKey>>  source,
                    Func<TObject, TKey, IObservable<TAny>>  reevaluator,
                    TimeSpan?                               changeSetBuffer = null,
                    IScheduler?                             scheduler       = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return (changeSetBuffer is null)
            ? Observable.Create<IChangeSet<TObject, TKey>>(downstreamObserver => AutoRefreshOnObservable1Subscription
                .WithoutBuffering<TObject, TKey, TAny>.CreateAndActivate(
                    downstreamObserver:         downstreamObserver,
                    refreshRequestedFactory:    reevaluator,
                    source:                     source))
            : Observable.Create<IChangeSet<TObject, TKey>>(downstreamObserver => AutoRefreshOnObservable1Subscription
                .WithBuffering<TObject, TKey, TAny>.CreateAndActivate(
                    downstreamObserver:         downstreamObserver,
                    refreshBufferWindow:        changeSetBuffer.Value,
                    refreshRequestedFactory:    reevaluator,
                    scheduler:                  scheduler,
                    source:                     source));
    }

    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable1<TObject, TKey, TAny>(
            this    IObservable<IChangeSet<TObject, TKey>>  source,
                    Func<TObject, IObservable<TAny>>        reevaluator,
                    TimeSpan?                               changeSetBuffer = null,
                    IScheduler?                             scheduler       = null)
        where TObject : notnull
        where TKey : notnull
    {
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return source.AutoRefreshOnObservable1(
            changeSetBuffer:            changeSetBuffer,
            reevaluator:                (item, _) => reevaluator(item),
            scheduler:                  scheduler);
    }
}
