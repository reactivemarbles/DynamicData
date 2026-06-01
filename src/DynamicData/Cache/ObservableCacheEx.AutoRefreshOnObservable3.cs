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

using System.Reactive.Concurrency;

namespace DynamicData;

public static partial class ObservableCacheEx
{
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable3<TObject, TKey, TAny>(
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
            ? source.SubscribeManyBase<TObject, TKey, TAny, IChangeSet<TObject, TKey>>(orchestrator => new AutoRefreshOnObservable3Implementation
                .WithoutBuffering<TObject, TKey, TAny>(
                    orchestrator:               orchestrator,
                    refreshRequestedFactory:    reevaluator))
            : source.SubscribeManyBase<TObject, TKey, TAny, IChangeSet<TObject, TKey>>(orchestrator => AutoRefreshOnObservable3Implementation
                .WithBuffering<TObject, TKey, TAny>.CreateAndActivate(
                    orchestrator:               orchestrator,
                    refreshBufferWindow:        changeSetBuffer.Value,
                    refreshRequestedFactory:    reevaluator,
                    scheduler:                  scheduler));
    }

    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable3<TObject, TKey, TAny>(
            this    IObservable<IChangeSet<TObject, TKey>>  source,
                    Func<TObject, IObservable<TAny>>        reevaluator,
                    TimeSpan?                               changeSetBuffer = null,
                    IScheduler?                             scheduler       = null)
        where TObject : notnull
        where TKey : notnull
    {
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return source.AutoRefreshOnObservable3(
            changeSetBuffer:    changeSetBuffer,
            reevaluator:        (item, _) => reevaluator(item),
            scheduler:          scheduler);
    }
}
