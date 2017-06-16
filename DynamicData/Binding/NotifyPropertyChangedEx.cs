using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Binding
{
    /// <summary>
    /// Property changes notification
    /// </summary>
    public static class NotifyPropertyChangedEx
    {
        /// <summary>
        /// Notifies when any any property on the object has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertiesToMonitor">specify properties to Monitor, or omit to monitor all property changes</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<TObject> WhenAnyPropertyChanged<TObject>([NotNull] this TObject source, params string[] propertiesToMonitor)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                )
                .Where(x => propertiesToMonitor == null || propertiesToMonitor.Length == 0 || propertiesToMonitor.Contains(x.EventArgs.PropertyName))
                .Select(x => source);
        }

        /// <summary>
        /// Observes property changes for the specified property, starting with the current value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TProperty">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property to observe</param>
        /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value</param>
        /// <param name="fallbackValue"> A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">propertyAccessor</exception>
        public static IObservable<PropertyValue<TObject, TProperty>> WhenPropertyChanged<TObject, TProperty>([NotNull] this TObject source, 
            Expression<Func<TObject, TProperty>> propertyAccessor, 
            bool notifyOnInitialValue = true, 
            Func<TProperty> fallbackValue = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            var cache = ObservablePropertyFactoryCache.Instance.GetFactory(propertyAccessor);
            return cache.Create(source, notifyOnInitialValue)
                .Where(pv => !pv.UnobtainableValue || (pv.UnobtainableValue && fallbackValue != null));
        }

        /// <summary>
        /// Observes property changes for the specified property, starting with the current value
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property to observe</param>
        /// <param name="notifyOnInitialValue">If true the resulting observable includes the initial value</param>
        /// <param name="fallbackValue"> A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.</param>
        public static IObservable<TProperty> WhenValueChanged<TObject, TProperty>([NotNull] this TObject source, Expression<Func<TObject, TProperty>> propertyAccessor, bool notifyOnInitialValue = true, Func<TProperty> fallbackValue = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            return source.WhenChanged(propertyAccessor, notifyOnInitialValue, fallbackValue);
        }

        /// <summary>
        /// Produces an observable based on the combined values of the specifed properties, including the inital value.  
        /// 
        /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.
        /// </summary>
        public static IObservable<TResult> WhenChanged<TObject, TResult, TProperty1>([NotNull] this TObject source,
            Expression<Func<TObject, TProperty1>> p1,
            Func<TObject, TProperty1,  TResult> resultSelector,
            Func<TProperty1> p1Fallback = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return source.WhenChanged(p1, true, p1Fallback).Select(v => resultSelector(source, v));
        }

        /// <summary>
        /// Produces an observable based on the combined values of the specifed properties, including the inital value.  
        /// 
        /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.
        /// </summary>
        public static IObservable<TResult> WhenChanged<TObject, TResult, TProperty1, TProperty2>([NotNull] this TObject source, 
            Expression<Func<TObject, TProperty1>> p1,
            Expression<Func<TObject, TProperty2>> p2, 
            Func<TObject, TProperty1, TProperty2, TResult> resultSelector,
            Func<TProperty1> p1Fallback = null,
            Func<TProperty2> p2Fallback = null)
                where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return Observable.CombineLatest
            (
                source.WhenChanged(p1, true, p1Fallback),
                source.WhenChanged(p2, true, p2Fallback),
                (v1, v2) => resultSelector(source, v1, v2)
            );
        }

        /// <summary>
        /// Produces an observable based on the combined values of the specifed properties, including the inital value.  
        /// 
        /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.
        /// </summary>
        public static IObservable<TResult> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3>([NotNull] this TObject source,
            Expression<Func<TObject, TProperty1>> p1,
            Expression<Func<TObject, TProperty2>> p2,
            Expression<Func<TObject, TProperty3>> p3,
            Func<TObject, TProperty1, TProperty2, TProperty3, TResult> resultSelector,
            Func<TProperty1> p1Fallback = null,
            Func<TProperty2> p2Fallback = null,
            Func<TProperty3> p3Fallback = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            if (p3 == null) throw new ArgumentNullException(nameof(p3));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return Observable.CombineLatest
            (
                source.WhenChanged(p1, true, p1Fallback),
                source.WhenChanged(p2, true, p2Fallback),
                source.WhenChanged(p3, true, p3Fallback),
                (v1, v2, v3) => resultSelector(source, v1, v2, v3)
            );
        }

        /// <summary>
        /// Produces an observable based on the combined values of the specifed properties, including the inital value.  
        /// 
        /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.
        /// </summary>
        public static IObservable<TResult> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3, TProperty4>([NotNull] this TObject source,
            Expression<Func<TObject, TProperty1>> p1,
            Expression<Func<TObject, TProperty2>> p2,
            Expression<Func<TObject, TProperty3>> p3,
            Expression<Func<TObject, TProperty4>> p4,
            Func<TObject, TProperty1, TProperty2, TProperty3, TProperty4, TResult> resultSelector,
            Func<TProperty1> p1Fallback = null,
            Func<TProperty2> p2Fallback = null,
            Func<TProperty3> p3Fallback = null,
            Func<TProperty4> p4Fallback = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            if (p3 == null) throw new ArgumentNullException(nameof(p3));
            if (p4 == null) throw new ArgumentNullException(nameof(p4));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return Observable.CombineLatest
            (
                source.WhenChanged(p1, true, p1Fallback),
                source.WhenChanged(p2, true, p2Fallback),
                source.WhenChanged(p3, true, p3Fallback),
                source.WhenChanged(p4, true, p4Fallback),
                (v1, v2, v3, v4) => resultSelector(source, v1, v2, v3, v4)
            );
        }

        /// <summary>
        /// Produces an observable based on the combined values of the specifed properties, including the inital value.  
        /// 
        /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.
        /// </summary>
        public static IObservable<TResult> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3, TProperty4, TProperty5>([NotNull] this TObject source,
            Expression<Func<TObject, TProperty1>> p1,
            Expression<Func<TObject, TProperty2>> p2,
            Expression<Func<TObject, TProperty3>> p3,
            Expression<Func<TObject, TProperty4>> p4,
            Expression<Func<TObject, TProperty5>> p5,
            Func<TObject, TProperty1, TProperty2, TProperty3, TProperty4, TProperty5, TResult> resultSelector,
            Func<TProperty1> p1Fallback = null,
            Func<TProperty2> p2Fallback = null,
            Func<TProperty3> p3Fallback = null,
            Func<TProperty4> p4Fallback = null,
            Func<TProperty5> p5Fallback = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            if (p3 == null) throw new ArgumentNullException(nameof(p3));
            if (p4 == null) throw new ArgumentNullException(nameof(p4));
            if (p5 == null) throw new ArgumentNullException(nameof(p5));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return Observable.CombineLatest
            (
                source.WhenChanged(p1, true, p1Fallback),
                source.WhenChanged(p2, true, p2Fallback),
                source.WhenChanged(p3, true, p3Fallback),
                source.WhenChanged(p4, true, p4Fallback),
                source.WhenChanged(p5, true, p5Fallback),
                (v1, v2, v3, v4, v5) => resultSelector(source, v1, v2, v3, v4, v5)
            );
        }

        /// <summary>
        /// Produces an observable based on the combined values of the specifed properties, including the inital value.  
        /// 
        /// ** A fallback value may be specified to ensure a notification is received when a value is unobtainable. 
        /// For example when observing Parent.Child.Age, if Child == null the value is unobtainable as Age is a struct and cannot be set to Null. 
        /// For an object like Parent.Child.Sibling, sibling is an object so if Child == null, the value null and obtainable and is returned as null.
        /// </summary>
        public static IObservable<TResult> WhenChanged<TObject, TResult, TProperty1, TProperty2, TProperty3, TProperty4, TProperty5, TProperty6>([NotNull] this TObject source,
            Expression<Func<TObject, TProperty1>> p1,
            Expression<Func<TObject, TProperty2>> p2,
            Expression<Func<TObject, TProperty3>> p3,
            Expression<Func<TObject, TProperty4>> p4,
            Expression<Func<TObject, TProperty5>> p5,
            Expression<Func<TObject, TProperty6>> p6,
            Func<TObject, TProperty1, TProperty2, TProperty3, TProperty4, TProperty5, TProperty6, TResult> resultSelector,
            Func<TProperty1> p1Fallback = null,
            Func<TProperty2> p2Fallback = null,
            Func<TProperty3> p3Fallback = null,
            Func<TProperty4> p4Fallback = null,
            Func<TProperty5> p5Fallback = null,
            Func<TProperty6> p6Fallback = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (p1 == null) throw new ArgumentNullException(nameof(p1));
            if (p2 == null) throw new ArgumentNullException(nameof(p2));
            if (p3 == null) throw new ArgumentNullException(nameof(p3));
            if (p4 == null) throw new ArgumentNullException(nameof(p4));
            if (p5 == null) throw new ArgumentNullException(nameof(p5));
            if (p6 == null) throw new ArgumentNullException(nameof(p6));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return Observable.CombineLatest
            (
                source.WhenChanged(p1, true, p1Fallback),
                source.WhenChanged(p2, true, p2Fallback),
                source.WhenChanged(p3, true, p3Fallback),
                source.WhenChanged(p4, true, p4Fallback),
                source.WhenChanged(p5, true, p5Fallback),
                source.WhenChanged(p6, true, p6Fallback),
                (v1, v2, v3, v4, v5,v6) => resultSelector(source, v1, v2, v3, v4, v5, v6)
            );
        }


        internal static IObservable<TProperty> WhenChanged<TObject, TProperty>(this TObject source, Expression<Func<TObject, TProperty>> expression, bool notifyInitial = true, Func<TProperty> fallbackValue = null)
            where TObject : INotifyPropertyChanged
        {
            var factory = ObservablePropertyFactoryCache.Instance.GetFactory(expression);
            return factory.Create(source, notifyInitial)
                .Where(pv => !pv.UnobtainableValue || pv.UnobtainableValue && fallbackValue != null)
                .Select(pv =>
                {
                    if (pv.UnobtainableValue && fallbackValue != null)
                        return fallbackValue();

                    return pv.Value;
                });
        }

        internal static Func<TObject, bool, IObservable<PropertyValue<TObject, TProperty>>> GetFactory<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
            where TObject : INotifyPropertyChanged
        {
            var factory = ObservablePropertyFactoryCache.Instance.GetFactory(expression);
            return (t, initial) => factory.Create(t, initial);
        }

    }
}
