using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class Distinct<T, TValue>
    {
        private readonly IObservable<IChangeSet<T>> _source;              
        private readonly Func<T, TValue> _valueSelector;

        public Distinct([NotNull] IObservable<IChangeSet<T>> source,
            [NotNull] Func<T, TValue> valueSelector)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _valueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
        }

        public IObservable<IChangeSet<TValue>> Run()
        {
            return Observable.Create<IChangeSet<TValue>>(observer =>
            {
                var valueCounters = new Dictionary<TValue, int>();
                var result = new ChangeAwareList<TValue>();

                return _source.Transform<T, ItemWithMatch>((t, previous,idx ) =>
                    {
                        var previousValue = previous.ConvertOr(p => p.Value, () => default(TValue));

                        return new ItemWithMatch(t, _valueSelector(t), previousValue);
                    },true)
                    .Select(changes => Process(valueCounters, result, changes))
                    .NotEmpty()
                    .SubscribeSafe(observer);
            });
        }

        private IChangeSet<TValue> Process(Dictionary<TValue, int> values, ChangeAwareList<TValue> result, IChangeSet<ItemWithMatch> changes)
        {
            void AddAction(TValue value) => values.Lookup(value)
                .IfHasValue(count => values[value] = count + 1)
                .Else(() =>
                {
                    values[value] = 1;
                    result.Add(value);
                });

            void RemoveAction(TValue value)
            {
                var counter = values.Lookup(value);
                if (!counter.HasValue) return;

                //decrement counter
                var newCount = counter.Value - 1;
                values[value] = newCount;
                if (newCount != 0) return;

                //if there are none, then remove and notify
                result.Remove(value);
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
                        if (value.Equals(previous)) continue;

                        RemoveAction(previous);
                        AddAction(value);
                        break;
                        }
                    case ListChangeReason.Replace:
                        {
                            var value = change.Item.Current.Value;
                            var previous = change.Item.Previous.Value.Value;
                            if (value.Equals(previous)) continue;

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

        private sealed class ItemWithMatch : IEquatable<ItemWithMatch>
        {
            public T Item { get; }
            public TValue Value { get; }
            public TValue Previous { get; }

            public ItemWithMatch(T item, TValue value, TValue previousValue)
            {
                Item = item;
                Value = value;
                Previous = previousValue;
            }

            #region Equality

            public bool Equals(ItemWithMatch other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return EqualityComparer<T>.Default.Equals(Item, other.Item);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ItemWithMatch)obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<T>.Default.GetHashCode(Item);
            }

            /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.List.Internal.Filter`1.ItemWithMatch" /> objects are equal.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
            public static bool operator ==(ItemWithMatch left, ItemWithMatch right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.List.Internal.Filter`1.ItemWithMatch" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ItemWithMatch left, ItemWithMatch right)
            {
                return !Equals(left, right);
            }

            #endregion

            public override string ToString()
            {
                return $"{nameof(Item)}: {Item}, {nameof(Value)}: {Value}, {nameof(Previous)}: {Previous}";
            }
        }
    }
}
