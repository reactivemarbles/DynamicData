// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;

namespace DynamicData.Binding;

internal sealed class ObservablePropertyFactoryCache
{
    public static readonly ObservablePropertyFactoryCache Instance = new();

    private readonly ConcurrentDictionary<string, object> _factories = new();

    private ObservablePropertyFactoryCache()
    {
    }

    public ObservablePropertyFactory<TObject, TProperty> GetFactory<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
        where TObject : INotifyPropertyChanged
    {
        var key = expression.ToCacheKey();

        var result = _factories.GetOrAdd(
            key,
            _ =>
            {
                var memberChain = expression.GetMemberChain().ToArray();
                if (memberChain.Length == 1)
                {
                    return new ObservablePropertyFactory<TObject, TProperty>(expression);
                }

                var chain = memberChain.Select(m => new ObservablePropertyPart(m)).ToArray();
                var accessor = expression.Compile() ?? throw new ArgumentNullException(nameof(expression));

                return new ObservablePropertyFactory<TObject, TProperty>(accessor, chain);
            });

        return (ObservablePropertyFactory<TObject, TProperty>)result;
    }
}
