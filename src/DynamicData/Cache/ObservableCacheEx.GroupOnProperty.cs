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
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// <para>Groups the source using the property specified by the property selector. Groups are re-applied when the property value changed.</para>
    /// <para>When there are likely to be a large number of group property changes specify a throttle to improve performance.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to group by a property value.</param>
    /// <param name="propertySelector">The <see cref="Expression{Func{TObject, TGroupKey}}"/> property selector used to group the items.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> a time span that indicates the throttle to wait for property change events.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <returns>An observable which will emit immutable group change sets.</returns>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnProperty<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TGroupKey>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
        where TGroupKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertySelector.ThrowArgumentNullExceptionIfNull(nameof(propertySelector));

        return new GroupOnProperty<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }
}
