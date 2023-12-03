// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class Distinct<T, TValue>(IObservable<IChangeSet<T>> source, Func<T, TValue> valueSelector)
    where T : notnull
    where TValue : notnull
{
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    private readonly Func<T, TValue> _valueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));

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

    private sealed class ItemWithMatch(T item, TValue value, TValue? previousValue) : IEquatable<ItemWithMatch>
    {
        public T Item { get; } = item;

        public TValue? Previous { get; } = previousValue;

        public TValue Value { get; } = value;

        /// <summary>Returns a value that indicates whether the values of two <see cref="Filter{T}.ItemWithMatch" /> objects are equal.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
        public static bool operator ==(ItemWithMatch left, ItemWithMatch right) => Equals(left, right);

        /// <summary>Returns a value that indicates whether two <see cref="Filter{T}.ItemWithMatch" /> objects have different values.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
        public static bool operator !=(ItemWithMatch left, ItemWithMatch right) => !Equals(left, right);

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

        public override int GetHashCode() => Item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Item);

        public override string ToString() => $"{nameof(Item)}: {Item}, {nameof(Value)}: {Value}, {nameof(Previous)}: {Previous}";
    }
}
