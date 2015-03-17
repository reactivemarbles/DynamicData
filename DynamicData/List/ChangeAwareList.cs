using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData
{
	internal class ChangeAwareCollection<T> : ISupportsCapcity, IList<T>
	{
		private readonly List<T> _innerList = new List<T>();
		ChangeSet<T> _changes = new ChangeSet<T>();
		private bool _isMoving;
		
		
		public ChangeSet<T> CaptureChanges()
		{
			var copy = _changes;
			_changes = new ChangeSet<T>();
			return copy;
		}

		#region Range support
		
		public void AddRange(IEnumerable<T> collection)
		{
			var changes = _innerList.Select((t, index) => new Change<T>(ChangeReason.Add, t, index + Count));
			_changes.AddRange(changes);
			_innerList.AddRange(collection);
		} 

		#endregion

		#region Collection overrides

		protected virtual void ClearItems()
		{
			//add in reverse order as this will be more efficient for any consumers to reflect
			var changes = _innerList.Select((t, index) => new Change<T>(ChangeReason.Remove, t, index)).Reverse();
			_changes.AddRange(changes);
			_innerList.Clear();
		}

		protected virtual void InsertItem(int index, T item)
		{
			if (_isMoving) return;

			_changes.Add(new Change<T>(ChangeReason.Add, item, index));
			_innerList.Insert(index, item);
		}

		protected virtual void RemoveItem(int index)
		{
			if (_isMoving) return;

			var item = _innerList[index];
			_changes.Add(new Change<T>(ChangeReason.Remove, item, index));
			_innerList.RemoveAt(index);
		}

		protected virtual void SetItem(int index, T item)
		{
			if (_isMoving) return;

			var previous = _innerList[index];
			_changes.Add(new Change<T>(ChangeReason.Update, item, previous, index, index));
			_innerList[index]= item;
		}

		public virtual void  Move(T item, int destination)
		{
			var index = _innerList.IndexOf(item);
			Move(index, destination);
		}
		
		public virtual void Move(int original, int destination)
		{
			try
			{
				_isMoving = true;
				var item = _innerList[original];
				RemoveItem(original);
				InsertItem(destination, item);
				_changes.Add(new Change<T>(item, destination, original));
			}
			finally
			{
				_isMoving = false;
			}
		}

		#endregion

		#region ISupportsCapcity

		public int Capacity
		{
			get { return _innerList.Capacity; }
			set { _innerList.Capacity = value; }
		}

		public int Count => _innerList.Count;

		#endregion

		#region IList<T> implementation

		public void Clear()
		{
			ClearItems();
		}

		public bool Contains(T item)
		{
			return _innerList.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			_innerList.CopyTo(array, arrayIndex);
		}

		public int IndexOf(T item)
		{
			return _innerList.IndexOf(item);
		}

		public void Insert(int index, T item)
		{
			InsertItem(index, item);
		}

		public void RemoveAt(int index)
		{
			RemoveItem(index);
		}

		public void Add(T item)
		{
			InsertItem(_innerList.Count, item);
		}

		public bool Remove(T item)
		{
			var index = _innerList.IndexOf(item);
			if (index < 0) return false;
			RemoveItem(index);
			return true;
		}

		public T this[int index]
		{
			get { return _innerList[index]; }
			set { SetItem(index, value); }
		}

		public IEnumerator<T> GetEnumerator()
		{
			return _innerList.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool IsReadOnly => false;

		#endregion
	}
}