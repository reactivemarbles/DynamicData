// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the Distinct class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <typeparam name="TValue">The type of the TValue value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="valueSelector">The valueSelector value.</param>
internal sealed class Distinct<T, TValue>(IObservable<IChangeSet<T>> source, Func<T, TValue> valueSelector)
    where T : notnull
    where TValue : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// The _valueSelector field.
    /// </summary>
    private readonly Func<T, TValue> _valueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TValue>> Run() => Observable.Create<IChangeSet<TValue>>(
            observer =>
            {
                var valueCounters = new Dictionary<TValue, int>();
                var result = new ChangeAwareList<TValue>();

                return _source.Transform<T, ItemWithMatch>(
                    (t, previous, _) =>
                    {
                        var previousValue = previous.ConvertOr(p => p is null ? default : p.Value, () => default);

                        return new ItemWithMatch(t, _valueSelector(t), previousValue);
                    },
                    true).Select(changes => Process(valueCounters, result, changes)).NotEmpty().SubscribeSafe(observer);
            });

    /// <summary>
    /// Executes the Process operation.
    /// </summary>
    /// <param name="values">The values value.</param>
    /// <param name="result">The result value.</param>
    /// <param name="changes">The changes value.</param>
    /// <returns>The result of the operation.</returns>
    private static IChangeSet<TValue> Process(Dictionary<TValue, int> values, ChangeAwareList<TValue> result, IChangeSet<ItemWithMatch> changes)
    {
        void AddAction(TValue value) =>
            values.Lookup(value).IfHasValue(count => values[value] = count + 1).Else(
                () =>
                {
                    values[value] = 1;
                    result.Add(value);
                });

        void RemoveAction(TValue value)
        {
            var counter = values.Lookup(value);
            if (!counter.HasValue)
            {
                return;
            }

            // decrement counter
            var newCount = counter.Value - 1;
            values[value] = newCount;
            if (newCount != 0)
            {
                return;
            }

            // if there are none, then remove and notify
            result.Remove(value);
            values.Remove(value);
        }

        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Add:
                    {
                        var value = change.Item.Current.Value;
                        AddAction(value);
                        break;
                    }

                case ListChangeReason.AddRange:
                    {
                        change.Range.Select(item => item.Value).ForEach(AddAction);
                        break;
                    }

                case ListChangeReason.Refresh:
                    {
                        var value = change.Item.Current.Value;
                        var previous = change.Item.Current.Previous;
                        if (value.Equals(previous))
                        {
                            continue;
                        }

                        if (previous is not null)
                        {
                            RemoveAction(previous);
                        }

                        AddAction(value);
                        break;
                    }

                case ListChangeReason.Replace:
                    {
                        var value = change.Item.Current.Value;
                        var previous = change.Item.Previous.Value.Value;
                        if (value.Equals(previous))
                        {
                            continue;
                        }

                        RemoveAction(previous);
                        AddAction(value);
                        break;
                    }

                case ListChangeReason.Remove:
                    {
                        var previous = change.Item.Current.Value;
                        RemoveAction(previous);
                        break;
                    }

                case ListChangeReason.RemoveRange:
                    {
                        change.Range.Select(item => item.Value).ForEach(RemoveAction);
                        break;
                    }

                case ListChangeReason.Clear:
                    {
                        result.Clear();
                        values.Clear();
                        break;
                    }
            }
        }

        return result.CaptureChanges();
    }

/// <summary>
/// Provides members for the ItemWithMatch class.
/// </summary>
/// <param name="item">The item value.</param>
/// <param name="value">The value value.</param>
/// <param name="previousValue">The previousValue value.</param>
private sealed class ItemWithMatch(T item, TValue value, TValue? previousValue) : IEquatable<ItemWithMatch>
    {
        /// <summary>
        /// Gets the Item value.
        /// </summary>
        public T Item { get; } = item;

        /// <summary>
        /// Gets the Previous value.
        /// </summary>
        public TValue? Previous { get; } = previousValue;

        /// <summary>
        /// Gets the Value value.
        /// </summary>
        public TValue Value { get; } = value;

        /// <summary>Returns a value that indicates whether the values of two <c>Filter&lt;T&gt;.ItemWithMatch</c> objects are equal.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
        public static bool operator ==(ItemWithMatch left, ItemWithMatch right) => Equals(left, right);

        /// <summary>Returns a value that indicates whether two <c>Filter&lt;T&gt;.ItemWithMatch</c> objects have different values.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
        public static bool operator !=(ItemWithMatch left, ItemWithMatch right) => !Equals(left, right);

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Equals(ItemWithMatch? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<T>.Default.Equals(Item, other.Item);
        }

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result of the operation.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ItemWithMatch)obj);
        }

        /// <summary>
        /// Executes the GetHashCode operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override int GetHashCode() => Item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Item);

        /// <summary>
        /// Executes the ToString operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override string ToString() => $"{nameof(Item)}: {Item}, {nameof(Value)}: {Value}, {nameof(Previous)}: {Previous}";
    }
}
