// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <typeparam name="TAny">The type of evaluation.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to monitor for observable-driven refresh signals.</param>
    /// <param name="reevaluator">The <c>Func&lt;TObject, IObservable&lt;TAny&gt;&gt;</c> observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration. Batches multiple refresh signals into a single changeset, improving performance when many elements change in quick succession. This greatly increases performance when many elements require a refresh.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    /// <seealso><c>ObservableListEx.AutoRefreshOnObservable</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => source.AutoRefreshOnObservable((t, _) => reevaluator(t), changeSetBuffer, scheduler);

    /// <summary>
    /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <typeparam name="TAny">The type of evaluation.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to monitor for observable-driven refresh signals.</param>
    /// <param name="reevaluator">The <c>Func&lt;TObject, TKey, IObservable&lt;TAny&gt;&gt;</c> observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration. Batches multiple refresh signals into a single changeset, improving performance when many elements change in quick succession. This greatly increases performance when many elements require a refresh.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    /// <remarks>
    /// <para><b>Worth noting:</b> Per-item observable errors are silently ignored (not forwarded to the downstream observer). Only source stream errors propagate.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(reevaluator);

        return new AutoRefresh<TObject, TKey, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
    }
}
