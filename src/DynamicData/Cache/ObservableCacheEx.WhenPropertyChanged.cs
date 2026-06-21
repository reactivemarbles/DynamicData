// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
#if REACTIVE_SHIM
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
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
    /// Emits a <c>PropertyValue&lt;TObject, TValue&gt;</c> (item + property value) whenever the specified property
    /// changes on any item in the cache. Subscribes via <see cref="INotifyPropertyChanged"/> using MergeMany.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the monitored property.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe a specific property on items in.</param>
    /// <param name="propertyAccessor">A <c>Expression&lt;TDelegate&gt;</c> that expression selecting the property to monitor.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (the default), the current property value is emitted immediately for each item upon subscription.</param>
    /// <returns>An observable of <c>PropertyValue&lt;TObject, TValue&gt;</c> containing both the item and its property value.</returns>
    /// <remarks>
    /// <para>
    /// Per-item subscriptions are created on Add, replaced on Update, disposed on Remove. Errors from individual
    /// property subscriptions are silently ignored. The output is not a changeset stream. If you only need
    /// the value (not the owning item), use <c>WhenValueChanged&lt;TObject, TKey, TValue&gt;</c> instead.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to the specified property on the new item. If <c>notifyOnInitialValue</c> is true, the current value is emitted immediately.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's property subscription and subscribes to the new item.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's property subscription. No further emissions for this item.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions. The existing property subscription continues.</description></item>
    /// <item><term>OnError</term><description>Per-item property subscription errors are silently ignored. Source errors terminate the stream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>ObservableListEx.WhenPropertyChanged</c></seealso>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertyAccessor);

        return source.MergeMany(t => t.WhenPropertyChanged(propertyAccessor, notifyOnInitialValue));
    }
}
