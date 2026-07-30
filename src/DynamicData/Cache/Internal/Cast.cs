// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the Cast class.
/// </summary>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="converter">The converter value.</param>
internal sealed class Cast<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    /// <summary>
    /// The _converter field.
    /// </summary>
    private readonly Func<TSource, TDestination> _converter = converter ?? throw new ArgumentNullException(nameof(converter));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TSource, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TKey>> Run() => _source.Select(
            changes =>
            {
                var transformed = changes.ToConcreteType().Select(change => new Change<TDestination, TKey>(change.Reason, change.Key, _converter(change.Current), change.Previous.Convert(_converter), change.CurrentIndex, change.PreviousIndex));
                return new ChangeSet<TDestination, TKey>(transformed);
            });
}
