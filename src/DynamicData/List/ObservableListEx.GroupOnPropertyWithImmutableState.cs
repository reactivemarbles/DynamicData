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
    /// Groups items by a property value, automatically re-grouping when the specified property changes.
    /// Each group emits immutable snapshots (not live observable lists).
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TGroup">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to group by property value with immutable snapshots.</param>
    /// <param name="propertySelector"><see cref="Expression{TDelegate}"/> selecting the property whose value determines the group key.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of <see cref="List.IGrouping{TObject, TGroup}"/> immutable group snapshots.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Combines <see cref="AutoRefresh{TObject, TProperty}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// with <see cref="GroupWithImmutableState{TObject, TGroupKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroupKey}, IObservable{Unit}?)"/>.
    /// Unlike <see cref="GroupOnProperty{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>,
    /// this produces immutable snapshots per group rather than live inner observable lists.
    /// </para>
    /// </remarks>
    /// <seealso cref="GroupOnProperty{TObject, TGroup}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TGroup}}, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="GroupWithImmutableState{TObject, TGroupKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TGroupKey}, IObservable{Unit}?)"/>
    public static IObservable<IChangeSet<List.IGrouping<TObject, TGroup>>> GroupOnPropertyWithImmutableState<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TGroup>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TGroup : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertySelector);

        return new GroupOnPropertyWithImmutableState<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }
}
