using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;


namespace DynamicData.Binding
{
    internal static class ExpressionBuilder
    {
        internal static IObservable<PropertyValue<TObject, TProperty>> ObserveChain<TObject, TProperty>(this TObject source, Expression<Func<TObject, TProperty>> expression, bool notifyInitial = true)
            where TObject : INotifyPropertyChanged
        {
            var chain = source.CreatePropertyChain(expression, notifyInitial);
            return chain.CreateObservable();
        }

        private static PropertyChain<TObject, TProperty> CreatePropertyChain<TObject, TProperty>(this TObject source, Expression<Func<TObject, TProperty>> expression, bool notifyInitial = true)
            where TObject : INotifyPropertyChanged
        {
            MemberExpression outer = expression.Body as MemberExpression; 
            MemberExpression[] members = GetMembers(expression).ToArray();

            var chain = expression.GetMemberChain().ToArray();


            var propertyChangedMonitor = chain.Select(m => new PropertyChainPart(m)).ToArray();
            return new PropertyChain<TObject, TProperty>(source, expression, propertyChangedMonitor, notifyInitial);
        }

        private static IEnumerable<MemberExpression> GetMemberChain<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        {
            var memberExpression = expression.Body as MemberExpression;
            while (memberExpression != null)
            {
                if (memberExpression.Expression.NodeType != ExpressionType.Parameter)
                {
                    var parent = memberExpression.Expression;
                    yield return memberExpression.Update(Expression.Parameter(parent.Type));
                }
                else
                {
                    yield return memberExpression;
                }
                memberExpression = memberExpression.Expression as MemberExpression;
            }
        }
        //Require: Need a value accessor to get the value from the parent

        internal static Func<object, object> CreateValueAccessor(this MemberExpression source)
        {
            //create an expression which accepts the parent and returns the child
            var property = source.GetProperty();
            var method = property.GetMethod;
           
            //convert the parameter i.e. the declaring class to an object
            var parameter = Expression.Parameter(typeof(object));
            var converted = Expression.Convert(parameter, source.Expression.Type);

            //call the get value of the property and box it
            var propertyCall = Expression.Call(converted, method);
            var boxed = Expression.Convert(propertyCall, typeof(object));
            var accessorExpr = Expression.Lambda<Func<object, object>>(boxed, parameter);

            var accessor = accessorExpr.Compile();
            return accessor;
        }

        internal static Func<object, IObservable<Unit>> CreatePropertyChangedFactory(this MemberExpression source)
        {
           
            var property = source.GetProperty();
            var inpc = typeof(INotifyPropertyChanged).GetTypeInfo().IsAssignableFrom(property.DeclaringType.GetTypeInfo());

            return t =>
            {
                if (t == null) return Observable.Never<Unit>();
                if (!inpc) return Observable.Return(Unit.Default);

                return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                    (
                        handler => ((INotifyPropertyChanged) t).PropertyChanged += handler,
                        handler => ((INotifyPropertyChanged) t).PropertyChanged -= handler
                    )
                    .Where(args => args.EventArgs.PropertyName == property.Name)
                    .Select(args => Unit.Default);
            };
        }

        internal static Func<object, IObservable<PropertyValue<object,object>>> CreatePropertyValueChangedFactory(this MemberExpression source)
        {
            var property = source.GetProperty();
          
            var inpc = typeof(INotifyPropertyChanged).GetTypeInfo().IsAssignableFrom(property.DeclaringType.GetTypeInfo());
            var valueAccessor = CreateValueAccessor(source);
           
            return t =>
            {   
                if (t == null) return Observable.Never<PropertyValue<object, object>>();



                if (!inpc) return Observable.Return(new PropertyValue<object, object>(t, valueAccessor(t)));

                return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                    (
                        handler => ((INotifyPropertyChanged)t).PropertyChanged += handler,
                        handler => ((INotifyPropertyChanged)t).PropertyChanged -= handler
                    )
                    .Where(args => args.EventArgs.PropertyName == property.Name)
                    .Select(args => new PropertyValue<object, object>(args.Sender, valueAccessor(args.Sender)));
            };
        }

        public static IEnumerable<MemberExpression> GetMembers<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
        {
            var memberExpression = expression.Body as MemberExpression;
            while (memberExpression != null)
            {
                yield return memberExpression;
                memberExpression = memberExpression.Expression as MemberExpression;
            }
        }

        public static IEnumerable<MemberExpression> GetProperties<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
        {
            var memberExpression = expression.Body as MemberExpression;
            while (memberExpression != null)
            {
                yield return memberExpression;
                memberExpression = memberExpression.Expression as MemberExpression;
            }
        }

        internal static PropertyInfo GetProperty<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        {
            var property = expression.GetMember() as PropertyInfo;
            if (property == null)
                throw new ArgumentException("Not a property expression");

            return property;
        }

        internal static PropertyInfo GetProperty(this MemberExpression expression)
        {
            var property = expression.Member as PropertyInfo;
            if (property == null)
                throw new ArgumentException("Not a property expression");

            return property;
        }

        internal static MemberInfo GetMember<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
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
                throw new ArgumentException("Not a member access");

            return memberExpression.Member;
        }
    }
}
