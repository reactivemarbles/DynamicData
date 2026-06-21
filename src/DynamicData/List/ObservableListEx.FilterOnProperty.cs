// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Filters items based on a property value, automatically re-evaluating when the specified property changes on any item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to filter by property value.</param>
    /// <param name="propertySelector"><see cref="Expression{TDelegate}"/> selecting the property to monitor for changes.</param>
    /// <param name="predicate">A <see cref="Func{T, TResult}"/> predicate evaluated against the item to determine inclusion.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of items satisfying the predicate, re-evaluated on property changes.</returns>
    /// <remarks>
    /// <para>Deprecated. Use <see cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/> followed by <see cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/> instead.</para>
    /// </remarks>
    /// <seealso cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="Filter{T}(IObservable{IChangeSet{T}}, Func{T, bool})"/>
    [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
    public static IObservable<IChangeSet<TObject>> FilterOnProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertySelector);
        ArgumentExceptionHelper.ThrowIfNull(predicate);

        return new FilterOnProperty<TObject, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
    }
}
