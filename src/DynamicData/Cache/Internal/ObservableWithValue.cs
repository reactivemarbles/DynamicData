// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ObservableWithValue class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TValue">The type of the TValue value.</typeparam>
internal sealed class ObservableWithValue<TObject, TValue>
    where TValue : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableWithValue{TObject, TValue}"/> class.
    /// </summary>
    /// <param name="item">The item value.</param>
    /// <param name="source">The source value.</param>
    public ObservableWithValue(TObject item, IObservable<TValue> source)
    {
        Item = item;
        Observable = source.Do(value => LatestValue = value);
    }

    /// <summary>
    /// Gets the Item value.
    /// </summary>
    public TObject Item { get; }

    /// <summary>
    /// Gets the LatestValue value.
    /// </summary>
    public ReactiveUI.Primitives.Optional<TValue> LatestValue { get; private set; } = ReactiveUI.Primitives.Optional<TValue>.None;

    /// <summary>
    /// Gets the Observable value.
    /// </summary>
    public IObservable<TValue> Observable { get; }
}
