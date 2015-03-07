using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal sealed class DistinctCalculator<T, TValue>
	{
		private readonly IDictionary<TValue, int> _valueCounters = new Dictionary<TValue, int>();
		private readonly ChangeAwareCollection<TValue> _result = new ChangeAwareCollection<TValue>();

		public IChangeSet<TValue> Process(IChangeSet<ItemWithValue<T, TValue>> updates)
		{

			Action<TValue> addAction = value => _valueCounters.Lookup(value)
				.IfHasValue(count => _valueCounters[value] = count + 1)
				.Else(() =>
				{
					_valueCounters[value] = 1;
					_result.Add(value);
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
				_result.Remove(value);
			};

			updates.ForEach(change =>
			{
				switch (change.Reason)
				{
					case ChangeReason.Add:
					{
						var value = change.Current.Value;
						addAction(value);
						break;
					}
					case ChangeReason.Evaluate:
					case ChangeReason.Update:
					{
						var value = change.Current.Value;
						var previous = change.Previous.Value.Value;
						if (value.Equals(previous)) return;

						removeAction(previous);
						addAction(value);
						break;
					}
					case ChangeReason.Remove:
					{
						var previous = change.Current.Value;
						removeAction(previous);
						break;
					}
				}
			});
			return _result.CaptureChanges();
		}
	}
}