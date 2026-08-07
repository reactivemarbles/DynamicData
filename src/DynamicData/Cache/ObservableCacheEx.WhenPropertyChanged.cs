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
    /// Emits a <see cref="PropertyValue{TObject, TValue}"/> (item + property value) whenever the specified property
    /// changes on any item in the cache. Subscribes via <see cref="INotifyPropertyChanged"/> using MergeMany.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the monitored property.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe a specific property on items in.</param>
    /// <param name="propertyAccessor">A <see cref="Expression{TDelegate}"/> that expression selecting the property to monitor.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (the default), the current property value is emitted immediately for each item upon subscription.</param>
    /// <returns>An observable of <see cref="PropertyValue{TObject, TValue}"/> containing both the item and its property value.</returns>
    /// <remarks>
    /// <para>
    /// Per-item subscriptions are created on Add, replaced on Update, disposed on Remove. The output is not a
    /// changeset stream. If you only need the value (not the owning item), use <see cref="WhenValueChanged{TObject, TKey, TValue}"/> instead.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the specified property on the new item. If <c>notifyOnInitialValue</c> is true, the current value is emitted immediately.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's property subscription and subscribes to the new item.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's property subscription. No further emissions for this item.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The existing property subscription continues.</description></item>
    /// <item><term>OnError</term><description>Errors from any item's property subscription terminate the output stream. Source errors also terminate the stream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ObservableListEx.WhenPropertyChanged"/>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        return source.MergeMany(t => t.WhenPropertyChanged(propertyAccessor, notifyOnInitialValue));
    }
}
