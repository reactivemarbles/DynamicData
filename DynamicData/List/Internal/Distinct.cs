using System;
using System.Collections.Generic;
using System.Linq;
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
		private readonly ChangeAwareList<TValue> _result = new ChangeAwareList<TValue>();

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
					case ListChangeReason.Add:
					{
						var value = change.Item.Current.Value;
						addAction(value);
						break;
					}
					case ListChangeReason.AddRange:
						{
							change.Range.Select(item=>item.Value).ForEach(addAction);
							break;
						}
					//	case ListChangeReason.Evaluate:
					case ListChangeReason.Update:
					{
						var value = change.Item.Current.Value;
						var previous = change.Item.Previous.Value.Value;
						if (value.Equals(previous)) return;

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
							_result.Clear();
							_valueCounters.Clear();
                            break;
						}
				}
			});
			return _result.CaptureChanges();
		}
	}
}