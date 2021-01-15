// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
#if WINUI3UWP
using Microsoft.UI.Xaml.Data;
#else
using System.ComponentModel;
#endif
using System.Linq;
using System.Linq.Expressions;

namespace DynamicData.Binding
{
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
                        ObservablePropertyFactory<TObject, TProperty> factory;

                        var memberChain = expression.GetMemberChain().ToArray();
                        if (memberChain.Length == 1)
                        {
                            factory = new ObservablePropertyFactory<TObject, TProperty>(expression);
                        }
                        else
                        {
                            var chain = memberChain.Select(m => new ObservablePropertyPart(m)).ToArray();
                            var accessor = expression.Compile() ?? throw new ArgumentNullException(nameof(expression));
                            factory = new ObservablePropertyFactory<TObject, TProperty>(accessor, chain);
                        }

                        return factory;
                    });

            return (ObservablePropertyFactory<TObject, TProperty>)result;
        }
    }
}