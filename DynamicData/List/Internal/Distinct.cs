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

                return _source.Transform(t => new ItemWithValue<T, TValue>(t, _valueSelector(t)))
                    .Select(changes => Process(valueCounters, result, changes))
                    .NotEmpty()
                    .SubscribeSafe(observer);
            });
        }

        private IChangeSet<TValue> Process(Dictionary<TValue, int> values, ChangeAwareList<TValue> result, IChangeSet<ItemWithValue<T, TValue>> changes)
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
                            change.Range.Select(item => item.Value).ForEach((Action<TValue>) AddAction);
                            break;
                        }
                    //	case ListChangeReason.Evaluate:
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
    }
}
