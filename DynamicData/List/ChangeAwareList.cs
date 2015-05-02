using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData
{
	internal class ChangeAwareList<T> : ISupportsCapcity,  IExtendedList<T>
	{
		private readonly List<T> _innerList = new List<T>();
	    private ChangeSet<T> _changes = new ChangeSet<T>();

		public void ClearChanges()
		{
			_changes = new ChangeSet<T>();
		}


		public ChangeSet<T> CaptureChanges()
		{
			var copy = _changes;
			_changes = new ChangeSet<T>();
			return copy;
		}

		#region Range support

		public void AddRange(IEnumerable<T> collection)
		{
			var args = new Change<T>(ListChangeReason.AddRange, collection);

			if (args.Range.Count == 0) return;
			_changes.Add(args);
			_innerList.AddRange(args.Range);
		}
		
		public void InsertRange(IEnumerable<T> collection,int index)
		{
			var args = new Change<T>(ListChangeReason.AddRange, collection, index);
			if (args.Range.Count == 0) return;
			_changes.Add(args);
			_innerList.InsertRange(index, args.Range);
		}

		public void RemoveRange(int index,int count)
		{
			var toremove = _innerList.Skip(index).Take(count).ToList();
			if (toremove.Count == 0) return;
			var args = new Change<T>(ListChangeReason.RemoveRange, toremove, index);

			_changes.Add(args);
			_innerList.RemoveRange(index, count);
		}

		public  void Clear()
		{
			if (_innerList.Count == 0) return;
			var toremove = _innerList.ToList();
			var args = new Change<T>(ListChangeReason.Clear, toremove);
			_changes.Add(args);
			_innerList.Clear();
		}

		#endregion

		#region Collection overrides

		protected virtual void InsertItem(int index, T item)
		{
            //attempt to batch updates as it is much more efficient
		    var last = _changes.Last;

		    if (last.HasValue && last.Value.Reason == ListChangeReason.Add)
		    {
		        //begin a new batch
		        var firstOfBatch = _changes.Count - 1;
		        var previousItem = last.Value.Item;
                _changes[firstOfBatch]=new Change<T>(ListChangeReason.AddRange, new[] { previousItem.Current, item}, previousItem.CurrentIndex);
            }
            else if (last.HasValue && last.Value.Reason== ListChangeReason.AddRange)
            {
                //append to batch
                var range = last.Value.Range;
                var lastInsertIndex = range.Index + range.Count;
                if (lastInsertIndex == index)
                {
                    range.Add(item);
                }
                else
                {
                    _changes.Add(new Change<T>(ListChangeReason.Add, item, index));
                }
            }
            else
            {
                //first add, so cannot infer range
                _changes.Add(new Change<T>(ListChangeReason.Add, item, index));
            }
            //finally, add the item
			_innerList.Insert(index, item);
		}

		protected virtual void RemoveItem(int index)
		{
			var item = _innerList[index];

            //attempt to batch updates as it is much more efficient
            var last = _changes.Last;
            if (last.HasValue && last.Value.Reason == ListChangeReason.Remove)
            {
                //begin a new batch
                var firstOfBatch = _changes.Count - 1;
                _changes[firstOfBatch] = new Change<T>(ListChangeReason.RemoveRange, new[] { last.Value.Item.Current, item }, index);
            }
            else if (last.HasValue && last.Value.Reason == ListChangeReason.RemoveRange)
            {

                //add to the end of the previous batch
                var range = last.Value.Range;
                if (range.Index == index)
                {
                    //removed in order
                    range.Add(item);
                }
                else if (range.Index== index+1)
                {
                    //removed in reverse order
                    range.Add(item);
                    range.SetStartingIndex(index);
                }
                else
                {
                    _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
                }
            }
            else
            {
                //first add, so cannot infer range
                _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
            }
			_innerList.RemoveAt(index);
		}

		protected virtual void SetItem(int index, T item)
		{
			var previous = _innerList[index];
			_changes.Add(new Change<T>(ListChangeReason.Update, item, previous, index, index));
			_innerList[index] = item;
		}

		public virtual void Move(T item, int destination)
		{
			var index = _innerList.IndexOf(item);
			Move(index, destination);
		}

		public virtual void Move(int original, int destination)
		{
			var item = _innerList[original];
            _innerList.RemoveAt(original);
            _innerList.Insert(destination, item);
			_changes.Add(new Change<T>(item, destination, original));
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
