using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal sealed class OnBeingRemoved<T>
	{
		private readonly IObservable<IChangeSet<T>> _source;
		private readonly Action<T> _callback;
		private readonly List<T> _items = new List<T>();

		public OnBeingRemoved([NotNull] IObservable<IChangeSet<T>> source, [NotNull] Action<T> callback)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (callback == null) throw new ArgumentNullException("callback");
			_source = source;
			_callback = callback;
		}

		public IObservable<IChangeSet<T>> Run()
		{
			return Observable.Create<IChangeSet<T>>
				(
					observer =>
					{
						var subscriber = _source
							.Do(RegisterForRemoval, observer.OnError)
							.SubscribeSafe(observer);

						return Disposable.Create(() =>
						{
							subscriber.Dispose();
							_items.ForEach(t => _callback(t));
							_items.Clear();
						});
					});
		}


		private void RegisterForRemoval(IChangeSet<T> changes)
		{
			changes.ForEach(change =>
			{
				switch (change.Reason)
				{
					case ChangeReason.Update:
						change.Previous.IfHasValue(t => _callback(t));
						break;
					case ChangeReason.Remove:
						_callback(change.Current);
						break;
				}
			});
			_items.Clone(changes);
		}

	}
}