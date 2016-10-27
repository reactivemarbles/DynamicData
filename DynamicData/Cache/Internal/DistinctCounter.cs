using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class DistinctCounter<TObject, TKey, TValue>
    {
        private readonly Func<TObject, TValue> _valueSelector;
        private readonly IDictionary<TValue, int> _valueCounters = new Dictionary<TValue, int>();
        private readonly IDictionary<TKey, TValue> _itemCache = new Dictionary<TKey, TValue>();

        public DistinctCounter(Func<TObject, TValue> valueSelector)
        {
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            _valueSelector = valueSelector;
        }

        public IDistinctChangeSet<TValue> Calculate(IChangeSet<TObject, TKey> updates)
        {
            var result = new List<Change<TValue, TValue>>();

            Action<TValue> addAction = value => _valueCounters.Lookup(value)
                                                              .IfHasValue(count => _valueCounters[value] = count + 1)
                                                              .Else(() =>
                                                              {
                                                                  _valueCounters[value] = 1;
                                                                  result.Add(new Change<TValue, TValue>(ChangeReason.Add, value, value));
                                                              });

            Action<TValue> removeAction = value =>
            {
                var counter = _valueCounters.Lookup(value);
                if (!counter.HasValue) return;

                //decrement counter
                var newCount = counter.Value - 1;
                _valueCounters[value] = newCount;
                if (newCount != 0) return;

                //if there are none, then remove and notify
                _valueCounters.Remove(value);
                result.Add(new Change<TValue, TValue>(ChangeReason.Remove, value, value));
            };

            foreach (var change in updates)
            {
                var key = change.Key;
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        {
                            var value = _valueSelector(change.Current);
                            addAction(value);
                            _itemCache[key] = value;
                            break;
                        }
                    case ChangeReason.Evaluate:
                    case ChangeReason.Update:
                        {
                            var value = _valueSelector(change.Current);
                            var previous = _itemCache[key];
                            if (value.Equals(previous)) continue;

                            removeAction(previous);
                            addAction(value);
                            _itemCache[key] = value;
                            break;
                        }
                    case ChangeReason.Remove:
                        {
                            var previous = _itemCache[key];
                            removeAction(previous);
                            _itemCache.Remove(key);
                            break;
                        }
                }
            }
            return new DistinctChangeSet<TValue>(result);
        }
    }
}
