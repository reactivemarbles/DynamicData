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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            _source = source;
            _valueSelector = valueSelector;
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
            Action<TValue> addAction = value => values.Lookup(value)
                .IfHasValue(count => values[value] = count + 1)
                .Else(() =>
                {
                    values[value] = 1;
                    result.Add(value);
                });

            Action<TValue> removeAction = value =>
            {
                var counter = values.Lookup(value);
                if (!counter.HasValue) return;

                //decrement counter
                var newCount = counter.Value - 1;
                values[value] = newCount;
                if (newCount != 0) return;

                //if there are none, then remove and notify
                result.Remove(value);
            };

            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        {
                            var value = change.Item.Current.Value;
                            addAction(value);
                            break;
                        }
                    case ListChangeReason.AddRange:
                        {
                            change.Range.Select(item => item.Value).ForEach(addAction);
                            break;
                        }
                    //	case ListChangeReason.Evaluate:
                    case ListChangeReason.Replace:
                        {
                            var value = change.Item.Current.Value;
                            var previous = change.Item.Previous.Value.Value;
                            if (value.Equals(previous)) continue;

                            removeAction(previous);
                            addAction(value);
                            break;
                        }
                    case ListChangeReason.Remove:
                        {
                            var previous = change.Item.Current.Value;
                            removeAction(previous);
                            break;
                        }
                    case ListChangeReason.RemoveRange:
                        {
                            change.Range.Select(item => item.Value).ForEach(removeAction);
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
