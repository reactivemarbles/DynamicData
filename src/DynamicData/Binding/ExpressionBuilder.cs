// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;

namespace DynamicData.Binding;

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

    internal static Func<object, IObservable<Unit>> CreatePropertyChangedFactory(this Expression source)
    {
        if ((source is not MemberExpression { Member: PropertyInfo property })
            || !typeof(INotifyPropertyChanged).IsAssignableFrom(property.DeclaringType))
        {
            return static _ => Observable.Never<Unit>();
        }

        return target => Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                addHandler: handler => ((INotifyPropertyChanged)target).PropertyChanged += handler,
                removeHandler: handler => ((INotifyPropertyChanged)target).PropertyChanged -= handler)
            .Where(pattern => pattern.EventArgs.PropertyName == property.Name)
            .Select(static _ => Unit.Default);
    }

    internal static Func<object, object?> CreateInvoker(this Expression source)
    {
        switch (source)
        {
            case MemberExpression memberExpression:
                if (memberExpression.Member is not PropertyInfo property)
                    throw new ArgumentException($"Unable to parse expression: Member type {memberExpression.Member.MemberType} is not supported", nameof(source));

                if (property.GetMethod is null)
                    throw new ArgumentException($"Unable to parse expression: Property \"{property.Name}\" has no getter", nameof(source));

                if (property.GetMethod.IsStatic)
                    throw new ArgumentException($"Unable to parse expression: Property \"{property.Name}\" is static", nameof(source));

                return property.GetValue;

            case UnaryExpression { NodeType: ExpressionType.Convert } convertExpression:
                return (convertExpression.Type.IsGenericType
                        && (convertExpression.Type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                    ? static target => target
                    : target => Convert.ChangeType(
                        value: target,
                        conversionType: convertExpression.Type);

            case null:
                throw new ArgumentNullException(nameof(source));

            default:
                throw new ArgumentException($"Unable to parse expression: Node type {source.NodeType} not supported", nameof(source));
        }
    }

    internal static MemberInfo GetMember<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
    {
        if (expression is null)
        {
            throw new ArgumentException("Not a property expression");
        }

        return GetMemberInfo(expression);
    }

    internal static IEnumerable<Expression> SplitIntoSteps<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
    {
        var currentStep = expression.Body;
        while (currentStep is not null)
        {
            switch (currentStep)
            {
                case MemberExpression memberExpression:
                    yield return memberExpression;
                    currentStep = memberExpression.Expression;
                    break;

                case ParameterExpression:
                    yield break;

                case UnaryExpression { NodeType: ExpressionType.Convert } unaryExpression:
                    yield return unaryExpression;
                    currentStep = unaryExpression.Operand;
                    break;

                default:
                    throw new ArgumentException($"Unable to parse expression: Node type {currentStep.NodeType} is not supported", nameof(expression));
            }
        }
    }

    internal static PropertyInfo GetProperty<TObject, TProperty>(this Expression<Func<TObject, TProperty>> expression)
    {
        if (expression.GetMember() is not PropertyInfo property)
        {
            throw new ArgumentException("Not a property expression");
        }

        return property;
    }

    internal static PropertyInfo GetProperty(this MemberExpression expression)
    {
        if (expression.Member is not PropertyInfo property)
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
            var type = typeof(TObject);

            yield return type.Assembly.FullName;
            yield return type.FullName;
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
