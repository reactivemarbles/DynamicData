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

using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData;

public static partial class ObservableCacheEx
{
    /// <summary>
    /// Automatically publishes <see cref="ChangeReason.Refresh"/> changes for individual items, based on a given per-item re-evaluation stream.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source collection.</typeparam>
    /// <typeparam name="TKey">The type of key values in the source collection.</typeparam>
    /// <typeparam name="TAny">The type of notifications produced by the re-evaluation streams.</typeparam>
    /// <param name="source">The source change stream, whose items are to be automatically refreshed.</param>
    /// <param name="reevaluator">A factory method for constructing the re-evaluation stream for each item.</param>
    /// <param name="changeSetBuffer">An optional time span, to be used to buffer and batch re-evaluation notifications. Re-evaluation notifications occurring within this amount of time of each other are batched into a single downstream change set. This can greatly increase performance when many elements require a refresh. See <see cref="Observable.Buffer{TSource,TBufferClosing}(System.IObservable{TSource},System.Func{System.IObservable{TBufferClosing}})"/> for details. A value of <see langword="null"/> disables buffering.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> to be used when buffering notifications.</param>
    /// <returns>A copy of <paramref name="source"/>, with the additional automatically-generated <see cref="ChangeReason.Refresh"/> changes.</returns>
    /// <seealso cref="ObservableListEx.AutoRefreshOnObservable"/>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(
                this    IObservable<IChangeSet<TObject, TKey>>  source,
                        Func<TObject, TKey, IObservable<TAny>>  reevaluator,
                        TimeSpan?                               changeSetBuffer = null,
                        IScheduler?                             scheduler       = null)
            where TObject : notnull
            where TKey : notnull
        => source.AutoRefreshOnObservable1(
            reevaluator:        reevaluator,
            changeSetBuffer:    changeSetBuffer,
            scheduler:          scheduler);

    /// <inheritdoc cref="AutoRefreshOnObservable{TObject,TKey,TAny}(System.IObservable{DynamicData.IChangeSet{TObject,TKey}},System.Func{TObject,TKey,System.IObservable{TAny}},System.TimeSpan?,System.Reactive.Concurrency.IScheduler?)"/>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(
                this    IObservable<IChangeSet<TObject, TKey>>  source,
                        Func<TObject, IObservable<TAny>>        reevaluator,
                        TimeSpan?                               changeSetBuffer = null,
                        IScheduler?                             scheduler       = null)
            where TObject : notnull
            where TKey : notnull
        => source.AutoRefreshOnObservable1(
            reevaluator:        reevaluator,
            changeSetBuffer:    changeSetBuffer,
            scheduler:          scheduler);
}
