using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
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
        public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>(
            [NotNull] this TObject source,
            Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            var member = propertyAccessor.GetProperty();
            var accessor = propertyAccessor.Compile();

            return WhenPropertyChanged(source, accessor, member.Name, notifyOnInitialValue);
        }

        /// <summary>
	    /// Observes property changes for the specified property, starting with the current value
	    /// </summary>
	    /// <typeparam name="TObject">The type of the object.</typeparam>
	    /// <typeparam name="TValue">The type of the value.</typeparam>
	    /// <param name="source">The source.</param>
	    /// <param name="propertyAccessor">The property accessor.</param>
	    /// <param name="propertyName">The property name to observe.</param>
	    /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
	    /// <returns></returns>
	    /// <exception cref="System.ArgumentNullException">propertyAccessor</exception>
	    internal static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>([NotNull] this TObject source,
            Func<TObject, TValue> propertyAccessor, string propertyName, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            Func<PropertyValue<TObject, TValue>> factory = () => new PropertyValue<TObject, TValue>(source, propertyAccessor(source));

            var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                )
                .Where(args => args.EventArgs.PropertyName == propertyName)
                .Select(x => factory());
            
            if (!notifyOnInitialValue)
                return propertyChanged;

            var initial = Observable.Defer(() => Observable.Return(factory()));
            return initial.Concat(propertyChanged);
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

            var member = propertyAccessor.GetProperty();
            var accessor = propertyAccessor.Compile();

            var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                )
                .Where(args => args.EventArgs.PropertyName == member.Name)
                .Select(x => accessor(source));

            if (!notifyOnInitialValue)
                return propertyChanged;

            var initial = Observable.Defer(() => Observable.Return(accessor(source)));
            return initial.Concat(propertyChanged);
        }

        /// <summary>
        /// Observes property changes for the specified property, starting with the current value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="propertyName">The property name to observe.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<TValue> WhenValueChanged<TObject, TValue>([NotNull] this TObject source, Func<TObject, TValue> propertyAccessor, string propertyName, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                ).Where(args => args.EventArgs.PropertyName == propertyName)
                                            .Select(x => propertyAccessor(source));

            return !notifyOnInitialValue ? propertyChanged : propertyChanged.StartWith(propertyAccessor(source));
        }


        /// <summary>
        /// Notifies when any any property on the object has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<TObject> WhenAnyPropertyChanged<TObject>([NotNull] this TObject source)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                )
                .Where(x => x.EventArgs.PropertyName != "IsSelected")
                .Select(x => source);
        }



    }
}
