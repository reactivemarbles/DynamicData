// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
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
    /// Emits the property value whenever the specified property changes on any item in the cache.
    /// Like <see cref="WhenPropertyChanged{TObject, TKey, TValue}"/> but emits only the value, discarding the owning item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the monitored property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe a specific property value on items in.</param>
    /// <param name="propertyAccessor">A <see cref="Expression{TDelegate}"/> that expression selecting the property to monitor.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (the default), the current property value is emitted immediately for each item upon subscription.</param>
    /// <returns>An observable of property values. The owning item is not included; use <see cref="WhenPropertyChanged{TObject, TKey, TValue}"/> if you need it.</returns>
    /// <remarks>
    /// <para>
    /// Per-item subscriptions are created on Add, replaced on Update, disposed on Remove. Errors from individual
    /// property subscriptions are silently ignored. If you need to correlate a value back to its source item,
    /// use <see cref="WhenPropertyChanged{TObject, TKey, TValue}"/> which returns a <see cref="PropertyValue{TObject, TValue}"/> pair.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the specified property. If <c>notifyOnInitialValue</c> is true, the current value is emitted immediately.</description></item>
    /// <item><term>Update</term><description>Disposes the old subscription, subscribes to the new item's property.</description></item>
    /// <item><term>Remove</term><description>Disposes the property subscription.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnError</term><description>Per-item errors silently ignored. Source errors terminate the stream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="WhenPropertyChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="WhenAnyPropertyChanged{TObject, TKey}"/>
    /// <seealso cref="AutoRefresh{TObject, TKey, TProperty}(IObservable{IChangeSet{TObject, TKey}}, Expression{Func{TObject, TProperty}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableListEx.WhenValueChanged"/>
    public static IObservable<TValue?> WhenValueChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        return source.MergeMany(t => t.WhenChanged(propertyAccessor, notifyOnInitialValue));
    }
}
