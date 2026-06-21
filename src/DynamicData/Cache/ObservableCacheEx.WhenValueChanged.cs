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
    /// Emits the property value whenever the specified property changes on any item in the cache.
    /// Like <c>WhenPropertyChanged&lt;TObject, TKey, TValue&gt;</c> but emits only the value, discarding the owning item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the monitored property.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe a specific property value on items in.</param>
    /// <param name="propertyAccessor">A <c>Expression&lt;TDelegate&gt;</c> that expression selecting the property to monitor.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (the default), the current property value is emitted immediately for each item upon subscription.</param>
    /// <returns>An observable of property values. The owning item is not included; use <c>WhenPropertyChanged&lt;TObject, TKey, TValue&gt;</c> if you need it.</returns>
    /// <remarks>
    /// <para>
    /// Per-item subscriptions are created on Add, replaced on Update, disposed on Remove. Errors from individual
    /// property subscriptions are silently ignored. If you need to correlate a value back to its source item,
    /// use <c>WhenPropertyChanged&lt;TObject, TKey, TValue&gt;</c> which returns a <c>PropertyValue&lt;TObject, TValue&gt;</c> pair.
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
    /// <seealso><c>WhenPropertyChanged&lt;TObject, TKey, TValue&gt;</c></seealso>
    /// <seealso><c>WhenAnyPropertyChanged&lt;TObject, TKey&gt;</c></seealso>
    /// <seealso><c>AutoRefresh&lt;TObject, TKey, TProperty&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Expression&lt;Func&lt;TObject, TProperty&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>ObservableListEx.WhenValueChanged</c></seealso>
    public static IObservable<TValue?> WhenValueChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertyAccessor);

        return source.MergeMany(t => t.WhenChanged(propertyAccessor, notifyOnInitialValue));
    }
}
