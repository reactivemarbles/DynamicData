using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal sealed class OnBeingRemoved<T> : IDisposable
	{
		private readonly Action<T> _callback;
		private readonly List<T> _items = new List<T>();

		public OnBeingRemoved(Action<T> callback)
		{
			if (callback == null) throw new ArgumentNullException("callback");
			_callback = callback;
		}

		public void RegisterForRemoval(IChangeSet<T> changes)
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

		public void Dispose()
		{
			_items.ForEach(t => _callback(t));
			_items.Clear();
		}
	}
}