using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicData.Binding
{
    internal static class ExpressionBuilder
    {

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
