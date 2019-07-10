// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

namespace DynamicData.Binding
{
    internal sealed class ObservablePropertyFactoryCache
    {
        private readonly ConcurrentDictionary<string,object> _factories = new ConcurrentDictionary<string, object>();

        public static readonly ObservablePropertyFactoryCache Instance = new ObservablePropertyFactoryCache();

        private ObservablePropertyFactoryCache()
        {
        }

        public ObservablePropertyFactory<TObject, TProperty> GetFactory<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
            where TObject : INotifyPropertyChanged
        {
            var key = expression.ToCacheKey();

            var result = _factories.GetOrAdd(key, k =>
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
                    var accessor = expression?.Compile() ?? throw new ArgumentNullException(nameof(expression));
                    factory = new ObservablePropertyFactory<TObject, TProperty>(accessor, chain);
                }

                return factory;
            });

            return (ObservablePropertyFactory<TObject, TProperty>)result;
        }
    }
}