// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicData.Binding;

internal sealed class ObservablePropertyFactoryCache
{
    public static readonly ObservablePropertyFactoryCache Instance = new();

    private readonly ConcurrentDictionary<CacheKey, object> _factories = new();

    private ObservablePropertyFactoryCache()
    {
    }

    public ObservablePropertyFactory<TObject, TProperty> GetFactory<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
        where TObject : INotifyPropertyChanged
    {
        var steps = expression.SplitIntoSteps().ToArray();
        var key = new CacheKey(expression.Type, steps);

        var result = _factories.GetOrAdd(
            key,
            _ =>
            {
                if (steps.Length == 1)
                {
                    return new ObservablePropertyFactory<TObject, TProperty>(expression);
                }

                var chain = steps.Select(m => new ObservablePropertyPart(m)).ToArray();
                var accessor = expression.Compile() ?? throw new ArgumentNullException(nameof(expression));

                return new ObservablePropertyFactory<TObject, TProperty>(accessor, chain);
            });

        return (ObservablePropertyFactory<TObject, TProperty>)result;
    }

    private sealed class CacheKey : IEquatable<CacheKey>
    {
        private readonly Type _delegateType;
        private readonly (ExpressionType NodeType, Type ResultType, Type? SourceType, MemberInfo? Member)[] _steps;

        public CacheKey(Type delegateType, Expression[] steps)
        {
            _delegateType = delegateType;
            _steps = new (ExpressionType, Type, Type?, MemberInfo?)[steps.Length];

            for (var i = 0; i < steps.Length; i++)
            {
                _steps[i] = steps[i] switch
                {
                    MemberExpression member => (member.NodeType, member.Type, member.Expression?.Type, member.Member),
                    UnaryExpression conversion => (conversion.NodeType, conversion.Type, conversion.Operand.Type, conversion.Method),
                    _ => throw new ArgumentException($"Unsupported property-path node: {steps[i].NodeType}", nameof(steps))
                };
            }
        }

        public bool Equals(CacheKey? other) =>
            other is not null && _delegateType == other._delegateType && _steps.SequenceEqual(other._steps);

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = _delegateType.GetHashCode();
            foreach (var step in _steps)
            {
                hash = unchecked((hash * 397) ^ step.GetHashCode());
            }

            return hash;
        }
    }
}
