// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableCache extensions for AutoRefresh.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Automatically refresh downstream operators when any properties change.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to monitor for property-driven refresh signals.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration. Batches multiple refresh signals into a single changeset, improving performance when many elements change in quick succession. This greatly increases performance when many elements have successive property changes.</param>
    /// <param name="propertyChangeThrottle">An optional <see cref="TimeSpan"/> throttle applied to each item's property change notifications, preventing excessive refresh invocations.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    /// <seealso cref="ObservableListEx.AutoRefresh"/>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefresh<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.AutoRefreshOnObservable(
            (t, _) =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenAnyPropertyChanged();
                }

                return t.WhenAnyPropertyChanged().Throttle(propertyChangeThrottle.Value, scheduler ?? GlobalConfig.DefaultScheduler);
            },
            changeSetBuffer,
            scheduler);
    }

    /// <summary>
    /// Automatically refresh downstream operators when properties change.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to monitor for property-driven refresh signals.</param>
    /// <param name="propertyAccessor">A <see cref="Expression{TDelegate}"/> that specify a property to observe changes. When it changes a Refresh is invoked.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration. Batches multiple refresh signals into a single changeset, improving performance when many elements change in quick succession. This greatly increases performance when many elements have successive property changes.</param>
    /// <param name="propertyChangeThrottle">An optional <see cref="TimeSpan"/> throttle applied to each item's property change notifications, preventing excessive refresh invocations.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefresh<TObject, TKey, TProperty>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TProperty>> propertyAccessor, TimeSpan? changeSetBuffer = null, TimeSpan? propertyChangeThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.AutoRefreshOnObservable(
            (t, _) =>
            {
                if (propertyChangeThrottle is null)
                {
                    return t.WhenPropertyChanged(propertyAccessor, false);
                }

                return t.WhenPropertyChanged(propertyAccessor, false).Throttle(propertyChangeThrottle.Value, scheduler ?? GlobalConfig.DefaultScheduler);
            },
            changeSetBuffer,
            scheduler);
    }

    /// <summary>
    /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <typeparam name="TAny">The type of evaluation.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to monitor for observable-driven refresh signals.</param>
    /// <param name="reevaluator">The <see cref="Func{TObject, IObservable{TAny}}"/> observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
    /// <param name="changeSetBuffer">An optional <see cref="TimeSpan"/> buffer duration. Batches multiple refresh signals into a single changeset, improving performance when many elements change in quick succession. This greatly increases performance when many elements require a refresh.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable change set with additional refresh changes.</returns>
    /// <seealso cref="ObservableListEx.AutoRefreshOnObservable"/>
    public static IObservable<IChangeSet<TObject, TKey>> AutoRefreshOnObservable<TObject, TKey, TAny>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TAny>> reevaluator, TimeSpan? changeSetBuffer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => source.AutoRefreshOnObservable((t, _) => reevaluator(t), changeSetBuffer, scheduler);

    /// <summary>
    /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification.
    /// </summary>
    /// <typeparam name="TObject">The object of the change set.</typeparam>
    /// <typeparam name="TKey">The key of the change set.</typeparam>
    /// <typeparam name="TAny">The type of evaluation.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to monitor for observable-driven refresh signals.</param>
    /// <param name="reevaluator">The <see cref="Func{TObject, TKey, IObservable{TAny}}"/> observable which acts on items within the collection and produces a value when the item should be refreshed.</param>
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        reevaluator.ThrowArgumentNullExceptionIfNull(nameof(reevaluator));

        return new AutoRefresh<TObject, TKey, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
    }
}
