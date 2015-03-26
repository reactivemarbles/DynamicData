using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal sealed class Distinct<T, TValue>
	{
		private readonly IObservable<IChangeSet<T>> _source;
		private readonly Func<T, TValue> _valueSelector;

		private readonly IDictionary<TValue, int> _valueCounters = new Dictionary<TValue, int>();
		private readonly ChangeAwareCollection<TValue> _result = new ChangeAwareCollection<TValue>();

		public Distinct([NotNull] IObservable<IChangeSet<T>> source,
			[NotNull] Func<T, TValue> valueSelector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (valueSelector == null) throw new ArgumentNullException("valueSelector");
			_source = source;
			_valueSelector = valueSelector;
		}

		public IObservable<IChangeSet<TValue>> Run()
		{
			return _source.Transform(t => new ItemWithValue<T, TValue>(t, _valueSelector(t)))
							.Select(Process)
							.NotEmpty();
		}

		private IChangeSet<TValue> Process(IChangeSet<ItemWithValue<T, TValue>> updates)
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