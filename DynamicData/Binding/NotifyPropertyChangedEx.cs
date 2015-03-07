using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;

namespace DynamicData.Binding
{
	/// <summary>
	/// Property changes notification
	/// </summary>
	public static class NotifyPropertyChangedEx
	{
		/// <summary>
		/// Observes property changes for the sepcifed property, starting with the current value
		/// </summary>
		/// <typeparam name="TObject">The type of the object.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="propertyAccessor">The property accessor.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">propertyAccessor</exception>
		public static IObservable<PropertyValue<TObject, TValue>> ObservePropertyValue<TObject, TValue>(this TObject source,
			Expression<Func<TObject, TValue>> propertyAccessor)
			where TObject : INotifyPropertyChanged
		{
			if (propertyAccessor == null) throw new ArgumentNullException("propertyAccessor");

			var member = propertyAccessor.GetProperty();
			var accessor = propertyAccessor.Compile();

			Func<PropertyValue<TObject, TValue>> factory =
				() => new PropertyValue<TObject, TValue>(source, accessor(source));

			var propertyChanged = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
				(
					handler => source.PropertyChanged += handler,
					handler => source.PropertyChanged -= handler
				)
				.Where(args => args.EventArgs.PropertyName == member.Name)
				.Select(x => factory())
				.StartWith(factory());

			return propertyChanged;
		}



		private static PropertyInfo GetProperty<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
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