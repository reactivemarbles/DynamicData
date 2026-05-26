// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableList extensions for property-change observation.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Watches all items in the source list and emits the item when any of its properties change.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <see cref="IObservable{T}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to observe property changes on items in.</param>
    /// <param name="propertiesToMonitor">An optional list of property names to monitor. If empty, all property changes are observed.</param>
    /// <returns>An observable emitting the item whenever any monitored property changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Implemented via <see cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>. Subscriptions are managed per item: created on add, disposed on remove.</para>
    /// </remarks>
    /// <seealso cref="WhenPropertyChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="WhenValueChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableCacheEx.WhenAnyPropertyChanged{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, string[])"/>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject>(this IObservable<IChangeSet<TObject>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }

    /// <summary>
    /// Watches a specific property on all items in the source list and emits a <see cref="PropertyValue{TObject, TValue}"/> (item + value pair) when it changes.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <see cref="IObservable{T}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of item. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to observe a specific property on items in.</param>
    /// <param name="propertyAccessor">An <see cref="Expression{TDelegate}"/> expression selecting the property to observe.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (default), the current value is emitted immediately upon subscribing to each item.</param>
    /// <returns>An observable emitting <see cref="PropertyValue{TObject, TValue}"/> whenever the property changes on any tracked item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertyAccessor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Implemented via <see cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>.</para>
    /// </remarks>
    /// <seealso cref="WhenValueChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="WhenAnyPropertyChanged{TObject}(IObservable{IChangeSet{TObject}}, string[])"/>
    /// <seealso cref="ObservableCacheEx.WhenPropertyChanged{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Expression{Func{TObject, TValue}}, bool)"/>
    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        var factory = propertyAccessor.GetFactory();
        return source.MergeMany(t => factory(t, notifyOnInitialValue));
    }

    /// <summary>
    /// Watches a specific property on all items and emits just the property value (without the sender) when it changes.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <see cref="IObservable{T}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of item. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to observe a specific property value on items in.</param>
    /// <param name="propertyAccessor">An <see cref="Expression{TDelegate}"/> expression selecting the property to observe.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (default), the current value is emitted immediately upon subscribing to each item.</param>
    /// <returns>An observable emitting the property value whenever it changes on any tracked item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertyAccessor"/> is <see langword="null"/>.</exception>
    /// <seealso cref="WhenPropertyChanged{TObject, TValue}(IObservable{IChangeSet{TObject}}, Expression{Func{TObject, TValue}}, bool)"/>
    /// <seealso cref="WhenAnyPropertyChanged{TObject}(IObservable{IChangeSet{TObject}}, string[])"/>
    /// <seealso cref="ObservableCacheEx.WhenValueChanged{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Expression{Func{TObject, TValue}}, bool)"/>
    public static IObservable<TValue?> WhenValueChanged<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        var factory = propertyAccessor.GetFactory();
        return source.MergeMany(t => factory(t, notifyOnInitialValue).Select(pv => pv.Value));
    }
}
