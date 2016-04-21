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
	        if (source == null)
	            throw new ArgumentNullException(nameof(source));
	        if (propertyAccessor == null)
	            throw new ArgumentNullException(nameof(propertyAccessor));

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
	    public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>(
	        [NotNull] this TObject source,
	        Func<TObject, TValue> propertyAccessor, string propertyName, bool notifyOnInitialValue = true)
	        where TObject : INotifyPropertyChanged
	    {
	        if (source == null)
	            throw new ArgumentNullException(nameof(source));
	        if (propertyAccessor == null)
	            throw new ArgumentNullException(nameof(propertyAccessor));

	        Func<PropertyValue<TObject, TValue>> factory = () => new PropertyValue<TObject, TValue>(source, propertyAccessor(source));

	        var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
	            (
	                handler => source.PropertyChanged += handler,
	                handler => source.PropertyChanged -= handler
	            )
	                                        .Where(args => args.EventArgs.PropertyName == propertyName)
	                                        .Select(x => factory());

	        return !notifyOnInitialValue ? propertyChanged : propertyChanged.StartWith(factory());
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

            return !notifyOnInitialValue ? propertyChanged : propertyChanged.StartWith(accessor(source));
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
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null)
                throw new ArgumentNullException(nameof(propertyAccessor));

            var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                ).Where(args => args.EventArgs.PropertyName == propertyName)
                                            .Select(x => propertyAccessor(source));

            return !notifyOnInitialValue ? propertyChanged : propertyChanged.StartWith(propertyAccessor(source));
        }

        /// <summary>
        /// Selects the value from the property changed notification
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        [Obsolete("Prefer WhenValueChanged or WhenPropertyChanged")]
        public static IObservable<TValue> Value<TObject, TValue>([NotNull] this IObservable<PropertyValue<TObject, TValue>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Select(prop => prop.Value);
        }

        /// <summary>
        /// Observes property changed for the specified object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        [Obsolete("Prefer WhenValueChanged or WhenPropertyChanged")]
        public static IObservable<string> ObservePropertyChanges<T>(this T source)
            where T : INotifyPropertyChanged
        {
            return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                )
                             .Select(x => x.EventArgs.PropertyName);
        }

	    internal static PropertyInfo GetProperty<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        {
            var property = GetMember(expression) as PropertyInfo;
            if (property == null)
                throw new ArgumentException("Not a property expression");

            return property;
        }

        private static MemberInfo GetMember<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        {
            if (expression == null)
                throw new ArgumentException("Not a property expression");

            return GetMemberInfo(expression);
        }

        private static MemberInfo GetMemberInfo(LambdaExpression lambda)
        {
            if (lambda == null)
                throw new ArgumentException("Not a property expression");

            MemberExpression memberExpression = null;
            if (lambda.Body.NodeType == ExpressionType.Convert)
            {
                memberExpression = ((UnaryExpression)lambda.Body).Operand as MemberExpression;
            }
            else if (lambda.Body.NodeType == ExpressionType.MemberAccess)
            {
                memberExpression = lambda.Body as MemberExpression;
            }
            else if (lambda.Body.NodeType == ExpressionType.Call)
            {
                return ((MethodCallExpression)lambda.Body).Method;
            }

            if (memberExpression == null)
            {
                throw new ArgumentException("Not a member access");
            }

            return memberExpression.Member;
        }
    }
}
