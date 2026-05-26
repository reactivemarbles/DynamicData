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
/// ObservableCache extensions for property-change observation.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Filters the source changeset stream to a single key, emitting each <see cref="Change{TObject, TKey}"/> for that key.
    /// Changes for all other keys are ignored.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>An observable of <see cref="Change{TObject, TKey}"/> for the specified key only.</returns>
    /// <remarks>
    /// <para>
    /// Emits Add, Update, Remove, and Refresh changes as they occur for the target key.
    /// No initial emission occurs if the key is not yet present in the cache. This operator does not
    /// produce changesets; it produces individual change notifications. For Optional-based watching,
    /// use <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>
    public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.SelectMany(updates => updates).Where(update => update.Key.Equals(key));
    }

    /// <summary>
    /// Filters the source changeset stream to a single key, emitting the current value each time it changes.
    /// Even emits the value on removal (the removed item's value).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservableCache{TObject, TKey}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>An observable of the item's value whenever it changes for the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>,
    /// this does not emit <see cref="Optional.None{T}"/> on removal. It emits the removed item's value instead.
    /// If you need to distinguish presence from absence, use ToObservableOptional.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits the added item's value.</description></item>
    /// <item><term>Update</term><description>Emits the new value.</description></item>
    /// <item><term>Remove</term><description>Emits the removed item's value (not <c>None</c>; use <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/> if you need removal detection).</description></item>
    /// <item><term>Refresh</term><description>Emits the current value.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. Changes to other keys are ignored entirely.</para>
    /// </remarks>
    /// <seealso cref="Watch{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Watch(key).Select(u => u.Current);
    }

    /// <inheritdoc cref="WatchValue{TObject, TKey}(IObservableCache{TObject, TKey}, TKey)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <remarks>This overload extends <see cref="IObservable{T}">IObservable</see>&lt;<see cref="IChangeSet{TObject, TKey}"/>&gt; instead of <see cref="IObservableCache{TObject, TKey}"/>.</remarks>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Watch(key).Select(u => u.Current);
    }

    /// <summary>
    /// Emits an item whenever any of its properties change via <see cref="INotifyPropertyChanged"/>.
    /// Subscribes to PropertyChanged on each cache item using MergeMany.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe property changes on items in.</param>
    /// <param name="propertiesToMonitor">The specific property names to monitor.  If empty, all property changes trigger emissions.</param>
    /// <returns>An observable that emits the item itself each time a monitored property changes.</returns>
    /// <remarks>
    /// <para>
    /// Subscriptions are managed per item: created on Add, replaced on Update, disposed on Remove.
    /// Errors from individual property subscriptions are silently ignored. The output is not a changeset
    /// stream; it is a plain <c>IObservable&lt;TObject?&gt;</c>. If the same item changes multiple properties
    /// rapidly, each change emits the item separately (no deduplication).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to PropertyChanged on the new item.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's subscription and subscribes to the new item.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's PropertyChanged subscription.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnError</term><description>Errors from individual property subscriptions are silently ignored. Source errors terminate the stream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="WhenPropertyChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="WhenValueChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="AutoRefresh{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableListEx.WhenAnyPropertyChanged"/>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }

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
    /// Per-item subscriptions are created on Add, replaced on Update, disposed on Remove. Errors from individual
    /// property subscriptions are silently ignored. The output is not a changeset stream. If you only need
    /// the value (not the owning item), use <see cref="WhenValueChanged{TObject, TKey, TValue}"/> instead.
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
    /// <seealso cref="ObservableListEx.WhenPropertyChanged"/>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        return source.MergeMany(t => t.WhenPropertyChanged(propertyAccessor, notifyOnInitialValue));
    }

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
