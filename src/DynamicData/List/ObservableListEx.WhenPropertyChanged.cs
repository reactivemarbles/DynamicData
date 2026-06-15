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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
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
    /// <para><b>Worth noting:</b> Prior to v9, per-item property subscription errors were silently ignored. As of v9 they propagate via the underlying <see cref="MergeMany{T, TDestination}(IObservable{IChangeSet{T}}, Func{T, IObservable{TDestination}})"/>.</para>
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
}
