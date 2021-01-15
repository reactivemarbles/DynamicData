// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if WINUI3UWP
using Microsoft.UI.Xaml.Data;
#else
using System.ComponentModel;
#endif
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;

namespace DynamicData.Binding
{
    internal static class ExpressionBuilder
    {
        public static IEnumerable<MemberExpression> GetMembers<TObject, TProperty>(this Expression<Func<TObject, TProperty>> source)
        {
            var memberExpression = source.Body as MemberExpression;
            while (memberExpression is not null)
            {
                yield return memberExpression;
                memberExpression = memberExpression.Expression as MemberExpression;
            }
        }

        internal static Func<object, IObservable<Unit>> CreatePropertyChangedFactory(this MemberExpression source)
        {
            var property = source.GetProperty();

            if (property.DeclaringType is null)
            {
                throw new ArgumentException("The property does not have a valid declaring type.", nameof(source));
            }

            var notifyPropertyChanged = typeof(INotifyPropertyChanged).GetTypeInfo().IsAssignableFrom(property.DeclaringType.GetTypeInfo());

            return t =>
                {
                    if (t is null)
                    {
                        return Observable<Unit>.Never;
                    }

                    if (!notifyPropertyChanged)
                    {
                        return Observable.Return(Unit.Default);
                    }

                    return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(handler => ((INotifyPropertyChanged)t).PropertyChanged += handler, handler => ((INotifyPropertyChanged)t).PropertyChanged -= handler).Where(args => args.EventArgs.PropertyName == property.Name).Select(_ => Unit.Default);
                };
        }

        internal static Func<object, object> CreateValueAccessor(this MemberExpression source)
        {
            // create an expression which accepts the parent and returns the child
            var property = source.GetProperty();
            var method = property.GetMethod;

            if (method is null)
            {
                throw new ArgumentException("The property does not have a valid get method.", nameof(source));
            }

            if (source.Expression is null)
            {
                throw new ArgumentException("The source expression does not have a valid expression.", nameof(source));
            }

            // convert the parameter i.e. the declaring class to an object
            var parameter = Expression.Parameter(typeof(object));
            var converted = Expression.Convert(parameter, source.Expression.Type);

            // call the get value of the property and box it
            var propertyCall = Expression.Call(converted, method);
            var boxed = Expression.Convert(propertyCall, typeof(object));
            var accessorExpr = Expression.Lambda<Func<object, object>>(boxed, parameter);

            return accessorExpr.Compile();
        }

        internal static MemberInfo GetMember<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        {
            if (expression is null)
            {
                throw new ArgumentException("Not a property expression");
            }

            return GetMemberInfo(expression);
        }

        internal static IEnumerable<MemberExpression> GetMemberChain<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        {
            var memberExpression = expression.Body as MemberExpression;
            while (memberExpression?.Expression is not null)
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

        internal static PropertyInfo GetProperty<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
        {
            var property = expression.GetMember() as PropertyInfo;
            if (property is null)
            {
                throw new ArgumentException("Not a property expression");
            }

            return property;
        }

        internal static PropertyInfo GetProperty(this MemberExpression expression)
        {
            var property = expression.Member as PropertyInfo;
            if (property is null)
            {
                throw new ArgumentException("Not a property expression");
            }

            return property;
        }

        internal static string ToCacheKey<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
            where TObject : INotifyPropertyChanged
        {
            var members = expression.GetMembers();

            IEnumerable<string?> GetNames()
            {
                yield return typeof(TObject).FullName;
                foreach (var member in members.Reverse())
                {
                    yield return member.Member.Name;
                }
            }

            return string.Join(".", GetNames());
        }

        private static MemberInfo GetMemberInfo(LambdaExpression lambda)
        {
            if (lambda is null)
            {
                throw new ArgumentException("Not a property expression");
            }

            MemberExpression? memberExpression = null;
            switch (lambda.Body.NodeType)
            {
                case ExpressionType.Convert when lambda.Body is UnaryExpression { Operand: MemberExpression unaryMemberExpression }:
                    memberExpression = unaryMemberExpression;
                    break;
                case ExpressionType.MemberAccess:
                    memberExpression = lambda.Body as MemberExpression;
                    break;
                case ExpressionType.Call:
                    return ((MethodCallExpression)lambda.Body).Method;
            }

            if (memberExpression is null)
            {
                throw new ArgumentException("Not a member access");
            }

            return memberExpression.Member;
        }
    }
}