using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A list which captures all changes which are made to it. These changes are recorded until CaptureChanges() at which point thw changes are cleared.
    /// 
    /// Used for creating custom operators
    /// </summary>
    /// <seealso cref="DynamicData.Kernel.ISupportsCapcity" />
    /// <seealso cref="DynamicData.IExtendedList{T}" />
    public class ChangeAwareList<T> : ISupportsCapcity, IExtendedList<T>
    {
        private readonly List<T> _innerList = new List<T>();
        private List<Change<T>> _changes = new List<Change<T>>();

        /// <summary>
        /// Create a changeset from recorded changes and clears known changes.
        /// </summary>
        public IChangeSet<T> CaptureChanges()
        {
            var copy = new ChangeSet<T>(_changes);
            _changes = new List<Change<T>>();

            //we can infer this is a Clear
            if (_innerList.Count == 0 && copy.Removes == copy.TotalChanges && copy.TotalChanges > 1)
            {
                var removed = copy.Unified().Select(u => u.Current);
                return new ChangeSet<T> { new Change<T>(ListChangeReason.Clear, removed) };
            }
            return copy;
        }

        /// <summary>
        /// Clears the changes (for testing).
        /// </summary>
        internal void ClearChanges()
        {
            _changes = new List<Change<T>>();
        }

        #region Range support
        
        /// <summary>
        /// Adds the elements of the specified collection to the end of the collection.
        /// </summary>
        /// <param name="collection">The collection whose elements should be added to the end of the <see cref="T:System.Collections.Generic.List`1" />. The collection itself cannot be null, but it can contain elements that are null, if type <paramref name="T" /> is a reference type.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="collection" /> is null.</exception>
        public void AddRange(IEnumerable<T> collection)
        {
            var args = new Change<T>(ListChangeReason.AddRange, collection);

            if (args.Range.Count == 0) return;
            _changes.Add(args);
            _innerList.AddRange(args.Range);
        }

        /// <summary>
        /// Inserts the elements of a collection into the <see cref="T:System.Collections.Generic.List`1" /> at the specified index.
        /// </summary>
        /// <param name="collection">The collection whose elements should be inserted into the <see cref="T:System.Collections.Generic.List`1" />. The collection itself cannot be null, but it can contain elements that are null, if type <paramref name="T" /> is a reference type.</param>
        /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="collection" /> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is less than 0.-or-<paramref name="index" /> is greater than <see cref="P:System.Collections.Generic.List`1.Count" />.</exception>
        public void InsertRange(IEnumerable<T> collection, int index)
        {
            var args = new Change<T>(ListChangeReason.AddRange, collection, index);
            if (args.Range.Count == 0) return;
            _changes.Add(args);
            _innerList.InsertRange(index, args.Range);
            OnInsertItems(index, args.Range);
        }

        /// <summary>
        /// Removes a range of elements from the <see cref="T:System.Collections.Generic.List`1"/>.
        /// </summary>
        /// <param name="index">The zero-based starting index of the range of elements to remove.</param><param name="count">The number of elements to remove.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.-or-<paramref name="count"/> is less than 0.</exception><exception cref="T:System.ArgumentException"><paramref name="index"/> and <paramref name="count"/> do not denote a valid range of elements in the <see cref="T:System.Collections.Generic.List`1"/>.</exception>

        public void RemoveRange(int index, int count)
        {
            if (index >= _innerList.Count || index + count > _innerList.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var toremove = _innerList.Skip(index).Take(count).ToList();
            if (toremove.Count == 0) return;
            var args = new Change<T>(ListChangeReason.RemoveRange, toremove, index);

            _changes.Add(args);
            _innerList.RemoveRange(index, count);
            OnRemoveItems(index, args.Range);
        }

        /// <summary>
        /// Removes all elements from the list
        /// </summary>
        public virtual void Clear()
        {
            if (_innerList.Count == 0) return;
            var toremove = _innerList.ToList();
            _changes.Add(new Change<T>(ListChangeReason.Clear, toremove));
            _innerList.Clear();
        }

        #endregion

        #region Subclass overrides

        protected virtual void OnSetItem(int index, T newItem, T oldItem)
        {
        }

        protected virtual void OnInsertItems(int startIndex, IEnumerable<T> items)
        {
        }

        protected virtual void OnRemoveItems(int startIndex, IEnumerable<T> items)
        {
        }

        #endregion

        #region Collection overrides

        /// <summary>
        /// Gets the last change in the collection
        /// </summary>
        private Optional<Change<T>> Last => _changes.Count == 0 ? Optional.None<Change<T>>() : _changes[_changes.Count - 1];

        protected virtual void InsertItem(int index, T item)
        {
            //attempt to batch updates as lists love to deal with ranges! (sorry if this code melts your mind)
            var last = Last;

            if (last.HasValue && last.Value.Reason == ListChangeReason.Add)
            {
                //begin a new batch if possible

                var firstOfBatch = _changes.Count - 1;
                var previousItem = last.Value.Item;

                if (index == previousItem.CurrentIndex)
                {
                    _changes[firstOfBatch] = new Change<T>(ListChangeReason.AddRange, new[] { item, previousItem.Current }, index);
                }
                else if (index == previousItem.CurrentIndex + 1)
                {
                    _changes[firstOfBatch] = new Change<T>(ListChangeReason.AddRange, new[] { previousItem.Current, item }, previousItem.CurrentIndex);
                }
                else
                {
                    _changes.Add(new Change<T>(ListChangeReason.Add, item, index));
                }
            }
            else if (last.HasValue && last.Value.Reason == ListChangeReason.AddRange)
            {
                //check whether the new item is in the specified range
                var range = last.Value.Range;

                var minimum = Math.Max(range.Index - 1, 0);
                var maximum = range.Index + range.Count;
                var isPartOfRange = index >= minimum && index <= maximum;

                if (!isPartOfRange)
                {
                    _changes.Add(new Change<T>(ListChangeReason.Add, item, index));
                }
                else
                {
                    var insertPosition = index - range.Index;
                    if (insertPosition < 0)
                    {
                        insertPosition = 0;
                    }
                    else if (insertPosition >= range.Count)
                    {
                        insertPosition = range.Count;
                    }
                    range.Insert(insertPosition, item);

                    if (range.Index == 4 && range.Count == 4)
                        Debug.WriteLine("");

                    if (index < range.Index)
                        range.SetStartingIndex(index);
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

        protected void RemoveItem(int index)
        {
            var item = _innerList[index];
            RemoveItem(index, item);
        }

        protected virtual void RemoveItem(int index, T item)
        {
            //attempt to batch updates as lists love to deal with ranges! (sorry if this code melts your mind)
            var last = Last;
            if (last.HasValue && last.Value.Reason == ListChangeReason.Remove)
            {
                //begin a new batch
                var firstOfBatch = _changes.Count - 1;
                var previousItem = last.Value.Item;

                if (index == previousItem.CurrentIndex)
                {
                    _changes[firstOfBatch] = new Change<T>(ListChangeReason.RemoveRange, new[] { previousItem.Current, item }, index);
                }

                else if (index == previousItem.CurrentIndex - 1)
                {
                    //Nb: double check this one as it is the same as clause above. Can it be correct?
                    _changes[firstOfBatch] = new Change<T>(ListChangeReason.RemoveRange, new[] { item, previousItem.Current }, index);
                }
                else
                {
                    _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
                }
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
                else if (range.Index == index - 1)
                {
                    _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
                }
                else if (range.Index == index + 1)
                {
                    //removed in reverse order
                    range.Insert(0, item);
                    range.SetStartingIndex(index);
                }
                else
                {
                    _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
                }
            }
            else
            {
                //first remove, so cannot infer range
                _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
            }
            _innerList.RemoveAt(index);
        }

        protected virtual void SetItem(int index, T item)
        {
            var previous = _innerList[index];
            _changes.Add(new Change<T>(ListChangeReason.Replace, item, previous, index, index));
            _innerList[index] = item;
            OnSetItem(index, item, previous);
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

        public int Capacity { get { return _innerList.Capacity; } set { _innerList.Capacity = value; } }

        public int Count => _innerList.Count;

        #endregion

        #region IList<T> implementation

        public virtual bool Contains(T item)
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

        public int IndexOf(T item, IEqualityComparer<T> equalityComparer)
        {
            return _innerList.IndexOf(item, equalityComparer);
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
            RemoveItem(index, item);
            return true;
        }

        public T this[int index] { get { return _innerList[index]; } set { SetItem(index, value); } }

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
