// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Aggregation;

/// <summary>
/// Accumulates values with a fold that has an exact inverse, such as a sum, so values can always be removed incrementally.
/// </summary>
/// <typeparam name="TValue">The type of the projected values.</typeparam>
/// <typeparam name="TState">The type of the accumulated state.</typeparam>
/// <param name="seed">The state of an empty collection.</param>
/// <param name="add">Folds a value into the state.</param>
/// <param name="remove">Folds a value out of the state.</param>
internal struct InvertibleAccumulator<TValue, TState>(TState seed, Func<TState, TValue, TState> add, Func<TState, TValue, TState> remove) : IAccumulator<TValue>
{
    private readonly TState _seed = seed;
    private readonly Func<TState, TValue, TState> _add = add;
    private readonly Func<TState, TValue, TState> _remove = remove;

    public TState State { get; private set; } = seed;

    public void Add(TValue value) => State = _add(State, value);

    public bool Remove(TValue value)
    {
        State = _remove(State, value);
        return true;
    }

    public bool Replace(TValue previous, TValue current)
    {
        State = _add(_remove(State, previous), current);
        return true;
    }

    public void Reset() => State = _seed;
}
