// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;

namespace DynamicData.Binding;

/// <summary>
/// Property changes notification.
/// </summary>
public static class NotifyPropertyChangedEx
{
    /// <summary>
    /// Notifies when any property on the object has changed.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertiesToMonitor">specify properties to Monitor, or omit to monitor all property changes.</param>
    /// <returns>A observable which includes notifying on any property.</returns>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject>(this TObject source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.FromEventPattern<PropertyChangedEventHandler?, PropertyChangedEventArgs>(handler => source.PropertyChanged += handler, handler => source.PropertyChanged -= handler).Where(x => propertiesToMonitor.Length == 0 || propertiesToMonitor.Contains(x.EventArgs.PropertyName)).Select(_ => source);
    }

    /// <summary>
    /// Produces an observable based on the combined values of the specified properties, including the initial value.
    /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TProperty1">The type of the first property.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="p1">An expression to the first property.</param>
    /// <param name="resultSelector">A function which will select the result from the properties and the source.</param>
    /// <param name="p1Fallback">Provides a fall back value for the first property in case of the property value cannot be obtained.</param>
    /// <returns>An observable which emits the results.</returns>
    public static IObservable<TResult?> WhenChanged<TObject, TResult, TProperty1>(this TObject source, Expression<Func<TObject, TProperty1>> p1, Func<TObject, TProperty1?, TResult> resultSelector, Func<TProperty1>? p1Fallback = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        p1.ThrowArgumentNullExceptionIfNull(nameof(p1));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.WhenChanged(p1, true, p1Fallback).Select(v => resultSelector(source, v));
    }

    /// <summary>
    /// Produces an observable based on the combined values of the specified properties, including the initial value.
    /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TProperty1">The type of the first property.</typeparam>
    /// <typeparam name="TProperty2">The type of the second property.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="p1">An expression to the first property.</param>
    /// <param name="p2">An expression to the second property.</param>
    /// <param name="resultSelector">A function which will select the result from the properties and the source.</param>
    /// <param name="p1Fallback">Provides a fall back value for the first property in case of the property value cannot be obtained.</param>
    /// <param name="p2Fallback">Provides a fall back value for the second property in case of the property value cannot be obtained.</param>
    /// <returns>An observable which emits the results.</returns>
    public static IObservable<TResult?> WhenChanged<TObject, TResult, TProperty1, TProperty2>(this TObject source, Expression<Func<TObject, TProperty1>> p1, Expression<Func<TObject, TProperty2>> p2, Func<TObject, TProperty1?, TProperty2?, TResult> resultSelector, Func<TProperty1>? p1Fallback = null, Func<TProperty2>? p2Fallback = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        p1.ThrowArgumentNullExceptionIfNull(nameof(p1));
        p2.ThrowArgumentNullExceptionIfNull(nameof(p2));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.WhenChanged(p1, true, p1Fallback).CombineLatest(source.WhenChanged(p2, true, p2Fallback), (v1, v2) => resultSelector(source, v1, v2));
    }

    /// <summary>
    /// Produces an observable based on the combined values of the specified properties, including the initial value.
    /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TProperty1">The type of the first property.</typeparam>
    /// <typeparam name="TProperty2">The type of the second property.</typeparam>
    /// <typeparam name="TProperty3">The type of the third property.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="p1">An expression to the first property.</param>
    /// <param name="p2">An expression to the second property.</param>
    /// <param name="p3">An expression to the third property.</param>
    /// <param name="resultSelector">A function which will select the result from the properties and the source.</param>
    /// <param name="p1Fallback">Provides a fall back value for the first property in case of the property value cannot be obtained.</param>
    /// <param name="p2Fallback">Provides a fall back value for the second property in case of the property value cannot be obtained.</param>
    /// <param name="p3Fallback">Provides a fall back value for the third property in case of the property value cannot be obtained.</param>
    /// <returns>An observable which emits the results.</returns>
    public static IObservable<TResult?> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3>(this TObject source, Expression<Func<TObject, TProperty1>> p1, Expression<Func<TObject, TProperty2>> p2, Expression<Func<TObject, TProperty3>> p3, Func<TObject, TProperty1?, TProperty2?, TProperty3?, TResult> resultSelector, Func<TProperty1>? p1Fallback = null, Func<TProperty2>? p2Fallback = null, Func<TProperty3>? p3Fallback = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        p1.ThrowArgumentNullExceptionIfNull(nameof(p1));
        p2.ThrowArgumentNullExceptionIfNull(nameof(p2));
        p3.ThrowArgumentNullExceptionIfNull(nameof(p3));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.WhenChanged(p1, true, p1Fallback).CombineLatest(source.WhenChanged(p2, true, p2Fallback), source.WhenChanged(p3, true, p3Fallback), (v1, v2, v3) => resultSelector(source, v1, v2, v3));
    }

    /// <summary>
    /// Produces an observable based on the combined values of the specified properties, including the initial value.
    /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TProperty1">The type of the first property.</typeparam>
    /// <typeparam name="TProperty2">The type of the second property.</typeparam>
    /// <typeparam name="TProperty3">The type of the third property.</typeparam>
    /// <typeparam name="TProperty4">The type of the fourth property.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="p1">An expression to the first property.</param>
    /// <param name="p2">An expression to the second property.</param>
    /// <param name="p3">An expression to the third property.</param>
    /// <param name="p4">An expression to the fourth property.</param>
    /// <param name="resultSelector">A function which will select the result from the properties and the source.</param>
    /// <param name="p1Fallback">Provides a fall back value for the first property in case of the property value cannot be obtained.</param>
    /// <param name="p2Fallback">Provides a fall back value for the second property in case of the property value cannot be obtained.</param>
    /// <param name="p3Fallback">Provides a fall back value for the third property in case of the property value cannot be obtained.</param>
    /// <param name="p4Fallback">Provides a fall back value for the fourth property in case of the property value cannot be obtained.</param>
    /// <returns>An observable which emits the results.</returns>
    public static IObservable<TResult?> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3, TProperty4>(this TObject source, Expression<Func<TObject, TProperty1>> p1, Expression<Func<TObject, TProperty2>> p2, Expression<Func<TObject, TProperty3>> p3, Expression<Func<TObject, TProperty4>> p4, Func<TObject, TProperty1?, TProperty2?, TProperty3?, TProperty4?, TResult> resultSelector, Func<TProperty1>? p1Fallback = null, Func<TProperty2>? p2Fallback = null, Func<TProperty3>? p3Fallback = null, Func<TProperty4>? p4Fallback = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        p1.ThrowArgumentNullExceptionIfNull(nameof(p1));
        p2.ThrowArgumentNullExceptionIfNull(nameof(p2));
        p3.ThrowArgumentNullExceptionIfNull(nameof(p3));
        p4.ThrowArgumentNullExceptionIfNull(nameof(p4));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.WhenChanged(p1, true, p1Fallback).CombineLatest(source.WhenChanged(p2, true, p2Fallback), source.WhenChanged(p3, true, p3Fallback), source.WhenChanged(p4, true, p4Fallback), (v1, v2, v3, v4) => resultSelector(source, v1, v2, v3, v4));
    }

    /// <summary>
    /// Produces an observable based on the combined values of the specified properties, including the initial value.
    /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TProperty1">The type of the first property.</typeparam>
    /// <typeparam name="TProperty2">The type of the second property.</typeparam>
    /// <typeparam name="TProperty3">The type of the third property.</typeparam>
    /// <typeparam name="TProperty4">The type of the fourth property.</typeparam>
    /// <typeparam name="TProperty5">The type of the fifth property.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="p1">An expression to the first property.</param>
    /// <param name="p2">An expression to the second property.</param>
    /// <param name="p3">An expression to the third property.</param>
    /// <param name="p4">An expression to the fourth property.</param>
    /// <param name="p5">An expression to the fifth property.</param>
    /// <param name="resultSelector">A function which will select the result from the properties and the source.</param>
    /// <param name="p1Fallback">Provides a fall back value for the first property in case of the property value cannot be obtained.</param>
    /// <param name="p2Fallback">Provides a fall back value for the second property in case of the property value cannot be obtained.</param>
    /// <param name="p3Fallback">Provides a fall back value for the third property in case of the property value cannot be obtained.</param>
    /// <param name="p4Fallback">Provides a fall back value for the fourth property in case of the property value cannot be obtained.</param>
    /// <param name="p5Fallback">Provides a fall back value for the fifth property in case of the property value cannot be obtained.</param>
    /// <returns>An observable which emits the results.</returns>
    public static IObservable<TResult?> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3, TProperty4, TProperty5>(this TObject source, Expression<Func<TObject, TProperty1>> p1, Expression<Func<TObject, TProperty2>> p2, Expression<Func<TObject, TProperty3>> p3, Expression<Func<TObject, TProperty4>> p4, Expression<Func<TObject, TProperty5>> p5, Func<TObject, TProperty1?, TProperty2?, TProperty3?, TProperty4?, TProperty5?, TResult> resultSelector, Func<TProperty1>? p1Fallback = null, Func<TProperty2>? p2Fallback = null, Func<TProperty3>? p3Fallback = null, Func<TProperty4>? p4Fallback = null, Func<TProperty5>? p5Fallback = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        p1.ThrowArgumentNullExceptionIfNull(nameof(p1));
        p2.ThrowArgumentNullExceptionIfNull(nameof(p2));
        p3.ThrowArgumentNullExceptionIfNull(nameof(p3));
        p4.ThrowArgumentNullExceptionIfNull(nameof(p4));
        p5.ThrowArgumentNullExceptionIfNull(nameof(p5));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.WhenChanged(p1, true, p1Fallback).CombineLatest(source.WhenChanged(p2, true, p2Fallback), source.WhenChanged(p3, true, p3Fallback), source.WhenChanged(p4, true, p4Fallback), source.WhenChanged(p5, true, p5Fallback), (v1, v2, v3, v4, v5) => resultSelector(source, v1, v2, v3, v4, v5));
    }

    /// <summary>
    /// Produces an observable based on the combined values of the specified properties, including the initial value.
    /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <typeparam name="TProperty1">The type of the first property.</typeparam>
    /// <typeparam name="TProperty2">The type of the second property.</typeparam>
    /// <typeparam name="TProperty3">The type of the third property.</typeparam>
    /// <typeparam name="TProperty4">The type of the fourth property.</typeparam>
    /// <typeparam name="TProperty5">The type of the fifth property.</typeparam>
    /// <typeparam name="TProperty6">The type of the sixth property.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="p1">An expression to the first property.</param>
    /// <param name="p2">An expression to the second property.</param>
    /// <param name="p3">An expression to the third property.</param>
    /// <param name="p4">An expression to the fourth property.</param>
    /// <param name="p5">An expression to the fifth property.</param>
    /// <param name="p6">An expression to the sixth property.</param>
    /// <param name="resultSelector">A function which will select the result from the properties and the source.</param>
    /// <param name="p1Fallback">Provides a fall back value for the first property in case of the property value cannot be obtained.</param>
    /// <param name="p2Fallback">Provides a fall back value for the second property in case of the property value cannot be obtained.</param>
    /// <param name="p3Fallback">Provides a fall back value for the third property in case of the property value cannot be obtained.</param>
    /// <param name="p4Fallback">Provides a fall back value for the fourth property in case of the property value cannot be obtained.</param>
    /// <param name="p5Fallback">Provides a fall back value for the fifth property in case of the property value cannot be obtained.</param>
    /// <param name="p6Fallback">Provides a fall back value for the sixth property in case of the property value cannot be obtained.</param>
    /// <returns>An observable which emits the results.</returns>
    public static IObservable<TResult?> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3, TProperty4, TProperty5, TProperty6>(this TObject source, Expression<Func<TObject, TProperty1>> p1, Expression<Func<TObject, TProperty2>> p2, Expression<Func<TObject, TProperty3>> p3, Expression<Func<TObject, TProperty4>> p4, Expression<Func<TObject, TProperty5>> p5, Expression<Func<TObject, TProperty6>> p6, Func<TObject, TProperty1?, TProperty2?, TProperty3?, TProperty4?, TProperty5?, TProperty6?, TResult> resultSelector, Func<TProperty1>? p1Fallback = null, Func<TProperty2>? p2Fallback = null, Func<TProperty3>? p3Fallback = null, Func<TProperty4>? p4Fallback = null, Func<TProperty5>? p5Fallback = null, Func<TProperty6>? p6Fallback = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        p1.ThrowArgumentNullExceptionIfNull(nameof(p1));
        p2.ThrowArgumentNullExceptionIfNull(nameof(p2));
        p3.ThrowArgumentNullExceptionIfNull(nameof(p3));
        p4.ThrowArgumentNullExceptionIfNull(nameof(p4));
        p5.ThrowArgumentNullExceptionIfNull(nameof(p5));
        p6.ThrowArgumentNullExceptionIfNull(nameof(p6));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.WhenChanged(p1, true, p1Fallback).CombineLatest(source.WhenChanged(p2, true, p2Fallback), source.WhenChanged(p3, true, p3Fallback), source.WhenChanged(p4, true, p4Fallback), source.WhenChanged(p5, true, p5Fallback), source.WhenChanged(p6, true, p6Fallback), (v1, v2, v3, v4, v5, v6) => resultSelector(source, v1, v2, v3, v4, v5, v6));
    }

    /// <summary>
    /// Observes property changes for the specified property, starting with the current value.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TProperty">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertyAccessor">The property to observe.</param>
    /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value.</param>
    /// <param name="fallbackValue"> A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.</param>
    /// <returns>A observable which also notifies when the property value changes.</returns>
    /// <exception cref="ArgumentNullException">propertyAccessor.</exception>
    public static IObservable<PropertyValue<TObject, TProperty>> WhenPropertyChanged<TObject, TProperty>(this TObject source, Expression<Func<TObject, TProperty>> propertyAccessor, bool notifyOnInitialValue = true, Func<TProperty?>? fallbackValue = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        var cache = ObservablePropertyFactoryCache.Instance.GetFactory(propertyAccessor);
        return cache.Create(source, notifyOnInitialValue).Where(pv => !pv.UnobtainableValue || (pv.UnobtainableValue && fallbackValue is not null));
    }

    /// <summary>
    /// Observes property changes for the specified property, starting with the current value.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TProperty">The type of the first property.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="propertyAccessor">The property to observe.</param>
    /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value.</param>
    /// <param name="fallbackValue"> A fallback value may be specified to ensure a notification is received when a value is unobtainable.
    /// For example when observing Parent.Child.Age, if Child is null the value is unobtainable as Age is a struct and cannot be set to Null.
    /// For an object like Parent.Child.Sibling, sibling is an object so if Child is null, the value null and obtainable and is returned as null.</param>
    /// <returns>An observable which emits the results.</returns>
    public static IObservable<TProperty?> WhenValueChanged<TObject, TProperty>(this TObject source, Expression<Func<TObject, TProperty>> propertyAccessor, bool notifyOnInitialValue = true, Func<TProperty>? fallbackValue = null)
        where TObject : INotifyPropertyChanged
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        propertyAccessor.ThrowArgumentNullExceptionIfNull(nameof(propertyAccessor));

        return source.WhenChanged(propertyAccessor, notifyOnInitialValue, fallbackValue);
    }

    internal static Func<TObject, bool, IObservable<PropertyValue<TObject, TProperty>>> GetFactory<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        where TObject : INotifyPropertyChanged
    {
        var factory = ObservablePropertyFactoryCache.Instance.GetFactory(expression);
        return (t, initial) => factory.Create(t, initial);
    }

    internal static IObservable<TProperty?> WhenChanged<TObject, TProperty>(this TObject source, Expression<Func<TObject, TProperty>> expression, bool notifyInitial = true, Func<TProperty?>? fallbackValue = null)
        where TObject : INotifyPropertyChanged
    {
        var factory = ObservablePropertyFactoryCache.Instance.GetFactory(expression);
        return factory.Create(source, notifyInitial).Where(pv => !pv.UnobtainableValue || (pv.UnobtainableValue && fallbackValue is not null)).Select(
            pv =>
            {
                if (pv.UnobtainableValue && fallbackValue is not null)
                {
                    return fallbackValue();
                }

                return pv.Value;
            });
    }
}
