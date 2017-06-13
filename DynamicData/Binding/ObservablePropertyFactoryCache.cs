using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using DynamicData.Kernel;

namespace DynamicData.Binding
{
    internal sealed class ObservablePropertyFactoryCache
    {
        private readonly IDictionary<string,object> _factories = new Dictionary<string, object>();
        private readonly object _locker = new object();

        public static readonly ObservablePropertyFactoryCache Instance = new ObservablePropertyFactoryCache();

        private ObservablePropertyFactoryCache()
        {
        }

        public ObservablePropertyFactory<TObject, TProperty> GetFactory<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
            where TObject : INotifyPropertyChanged
        {
            var key = expression.ToCacheKey();

            // ReSharper disable once InconsistentlySynchronizedField
            var exiting = _factories.Lookup(key);
            if (exiting.HasValue)
                return (ObservablePropertyFactory<TObject, TProperty>)exiting.Value;

            lock (_locker)
            {
                if (_factories.ContainsKey(key))
                    return (ObservablePropertyFactory<TObject, TProperty>) _factories[key];

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

                _factories[key] = factory;
                return factory;
            }
        }
    }
}