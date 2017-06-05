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
        /// Observes property changes for the specified property, starting with the current value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">propertyAccessor</exception>
        public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>([NotNull] this TObject source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            return source.ObserveChain(propertyAccessor, notifyOnInitialValue);
        }

        /// <summary>
        /// Observes property changes for the specified property, starting with the current value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<TValue> WhenValueChanged<TObject, TValue>([NotNull] this TObject source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            return source.ObserveChain(propertyAccessor, notifyOnInitialValue).Select(pv=>pv.Value);
        }

        internal static IObservable<PropertyValue<TObject, TProperty>> ObserveChain<TObject, TProperty>(this TObject source, Expression<Func<TObject, TProperty>> expression, bool notifyInitial = true)
            where TObject : INotifyPropertyChanged
        {
            var cache = ObservablePropertyFactoryCache.Instance.GetFactory(expression);
            return cache.Create(source, notifyInitial);
        }

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
                .Where(x => propertiesToMonitor==null || propertiesToMonitor.Length==0 || propertiesToMonitor.Contains(x.EventArgs.PropertyName))
                .Select(x => source);
        }
    }
}
