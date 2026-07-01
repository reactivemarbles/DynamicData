// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif

/// <summary>
/// Provides members for the ObservablePropertyFactoryCache class.
/// </summary>
internal sealed class ObservablePropertyFactoryCache
{
    /// <summary>
    /// The Instance field.
    /// </summary>
    public static readonly ObservablePropertyFactoryCache Instance = new();

    /// <summary>
    /// The _factories field.
    /// </summary>
    private readonly ConcurrentDictionary<string, object> _factories = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservablePropertyFactoryCache"/> class.
    /// </summary>
    private ObservablePropertyFactoryCache()
    {
    }

    /// <summary>
    /// Executes the GetFactory operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TProperty">The type of the TProperty value.</typeparam>
    /// <param name="expression">The expression value.</param>
    /// <returns>The result of the operation.</returns>
    public ObservablePropertyFactory<TObject, TProperty> GetFactory<TObject, TProperty>(Expression<Func<TObject, TProperty>> expression)
        where TObject : INotifyPropertyChanged
    {
        var key = expression.ToCacheKey();

        var result = _factories.GetOrAdd(
            key,
            _ =>
            {
                var steps = expression.SplitIntoSteps().ToArray();
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
}
