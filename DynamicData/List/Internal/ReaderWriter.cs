using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal sealed class ReaderWriter<T>
	{
		private readonly ChangeAwareCollection<T> _data = new ChangeAwareCollection<T>();
		private readonly object _locker = new object();
		
		public Continuation<IChangeSet<T>> Write(IChangeSet<T> changes)
		{
			if (changes == null) throw new ArgumentNullException("changes");
			IChangeSet<T> result;
			lock (_locker)
			{
				try
				{
					_data.Clone(changes);
					result = _data.CaptureChanges();
				}
				catch (Exception ex)
				{
					return new Continuation<IChangeSet<T>>(ex);
				}
			}
			return new Continuation<IChangeSet<T>>(result);
		}

		public Continuation<IChangeSet<T>> Write(Action<IList<T>> updateAction)
		{
			if (updateAction == null) throw new ArgumentNullException("updateAction");
			IChangeSet<T> result;
			lock (_locker)
			{
				try
				{
					updateAction(_data);
					result = _data.CaptureChanges();
				}
				catch (Exception ex)
				{
					return new Continuation<IChangeSet<T>>(ex);
				}
			}
			return new Continuation<IChangeSet<T>>(result);
		}

		public IEnumerable<T> Items
		{
			get
			{
				IEnumerable<T> result;
				lock (_locker)
				{
					result = _data.ToArray();
				}
				return result;
			}

		}
		public Optional<ItemWithIndex<T>> Lookup(T item, IEqualityComparer<T> equalityComparer=null)
		{
			lock (_locker)
			{
				return _data.Lookup(item, equalityComparer);
			}
		}

		public int Count => _data.Count;

	}
}