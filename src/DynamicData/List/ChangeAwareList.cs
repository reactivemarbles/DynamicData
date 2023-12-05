// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using DynamicData.Kernel;

namespace DynamicData;

/// <summary>
/// <para>A list which captures all changes which are made to it. These changes are recorded until CaptureChanges() at which point the changes are cleared.</para>
/// <para>Used for creating custom operators.</para>
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <seealso cref="IExtendedList{T}" />
public class ChangeAwareList<T> : IExtendedList<T>
    where T : notnull
{
    private readonly List<T> _innerList;
    private ChangeSet<T> _changes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareList{T}"/> class.
    /// Create a change aware list with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the internal lists.</param>
    public ChangeAwareList(int capacity = -1) =>
        _innerList = capacity > 0 ? new List<T>(capacity) : [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareList{T}"/> class.
    /// Create a change aware list with the specified items.
    /// </summary>
    /// <param name="items">The items to seed the change aware list with.</param>
    public ChangeAwareList(IEnumerable<T> items)
    {
        items.ThrowArgumentNullExceptionIfNull(nameof(items));

        var list = items.ToList();

        _innerList = new List<T>(list);

        if (_innerList.Count > 0)
        {
            _changes.Add(new Change<T>(ListChangeReason.AddRange, list));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeAwareList{T}"/> class.
    /// Clone an existing ChangeAwareList.
    /// </summary>
    /// <param name="list">The original ChangeAwareList to copy.</param>
    /// <param name="copyChanges">Should the list of changes also be copied over?.</param>
    public ChangeAwareList(ChangeAwareList<T> list, bool copyChanges)
    {
        list.ThrowArgumentNullExceptionIfNull(nameof(list));

        _innerList = new List<T>(list._innerList);

        if (copyChanges)
        {
            _changes = new ChangeSet<T>(list._changes);
        }
    }

    /// <summary>
    /// Gets or sets the total number of elements the internal data structure can hold without resizing.
    /// </summary>
    public int Capacity
    {
        get => _innerList.Capacity;
        set => _innerList.Capacity = value;
    }

    /// <summary>
    /// Gets the element count.
    /// </summary>
    public int Count => _innerList.Count;

    /// <summary>
    /// Gets a value indicating whether is this collection read only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets the last change in the collection.
    /// </summary>
    private Optional<Change<T>> Last => _changes.Count == 0 ? Optional.None<Change<T>>() : _changes[_changes.Count - 1];

    /// <summary>
    /// Gets or sets the item at the specified index.
    /// </summary>
    /// <param name="index">The index to set.</param>
    public T this[int index]
    {
        get => _innerList[index];
        set => SetItem(index, value);
    }

    /// <summary>
    /// Adds the item to the end of the collection.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item) => InsertItem(_innerList.Count, item);

    /// <summary>
    /// Adds the elements of the specified collection to the end of the collection.
    /// </summary>
    /// <param name="collection">The items to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection" /> is null.</exception>
    public void AddRange(IEnumerable<T> collection)
    {
        var args = new Change<T>(ListChangeReason.AddRange, collection, _innerList.Count);

        if (args.Range.Count == 0)
        {
            return;
        }

        _changes.Add(args);
        _innerList.AddRange(args.Range);
    }

    /// <summary>
    /// Create a change set from recorded changes and clears known changes.
    /// </summary>
    /// <returns>The change set.</returns>
    public IChangeSet<T> CaptureChanges()
    {
        if (_changes.Count == 0)
        {
            return ChangeSet<T>.Empty;
        }

        var returnValue = _changes;

        // we can infer this is a Clear
        if (_innerList.Count == 0 && returnValue.Removes == returnValue.TotalChanges && returnValue.TotalChanges > 1)
        {
            var removed = returnValue.Unified().Select(u => u.Current);
            returnValue = [new(ListChangeReason.Clear, removed)];
        }

        ClearChanges();

        return returnValue;
    }

    /// <summary>
    /// Removes all elements from the list.
    /// </summary>
    public virtual void Clear()
    {
        if (_innerList.Count == 0)
        {
            return;
        }

        var toRemove = _innerList.ToList();

        _changes.Add(new Change<T>(ListChangeReason.Clear, toRemove));
        _innerList.Clear();
    }

    /// <summary>
    /// Determines whether the element is in the collection.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>If the item is contained or not.</returns>
    public virtual bool Contains(T item) => _innerList.Contains(item);

    /// <summary>
    /// Copies the entire collection to a compatible one-dimensional array, starting at the specified index of the target array.
    /// </summary>
    /// <param name="array">The array to copy to.</param>
    /// <param name="arrayIndex">The index to start copying to.</param>
    public void CopyTo(T[] array, int arrayIndex) => _innerList.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _innerList.ToList().GetEnumerator();

    /// <summary>
    /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire collection.
    /// </summary>
    /// <param name="item">The item to get the index of.</param>
    /// <returns>The index.</returns>
    public int IndexOf(T item) => _innerList.IndexOf(item);

    /// <summary>
    /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire collection, using the specified comparer.
    /// </summary>
    /// <param name="item">The item to get the index of.</param>
    /// <param name="equalityComparer">The equality comparer to use to compare.</param>
    /// <returns>The index.</returns>
    public int IndexOf(T item, IEqualityComparer<T> equalityComparer) => _innerList.IndexOf(item, equalityComparer);

    /// <summary>
    /// Inserts an element into the list at the specified index.
    /// </summary>
    /// <param name="index">The index to insert at.</param>
    /// <param name="item">The item to insert.</param>
    public void Insert(int index, T item)
    {
        if (index < 0)
        {
            throw new ArgumentException($"{nameof(index)} cannot be negative");
        }

        if (index > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(index)} cannot be greater than the size of the collection");
        }

        InsertItem(index, item);
    }

    /// <summary>
    /// Inserts the elements of a collection into the <see cref="List{T}" /> at the specified index.
    /// </summary>
    /// <param name="collection">Inserts the specified items.</param>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> is less than 0.-or-<paramref name="index" /> is greater than <see cref="List{T}.Count" />.</exception>
    public void InsertRange(IEnumerable<T> collection, int index)
    {
        var args = new Change<T>(ListChangeReason.AddRange, collection, index);
        if (args.Range.Count == 0)
        {
            return;
        }

        _changes.Add(args);
        _innerList.InsertRange(index, args.Range);

        OnInsertItems(index, args.Range);
    }

    /// <summary>
    /// Moves the item to the specified destination index.
    /// </summary>
    /// <param name="item">The item to move.</param>
    /// <param name="destination">The destination index.</param>
    public virtual void Move(T item, int destination)
    {
        if (destination < 0)
        {
            throw new ArgumentException($"{nameof(destination)} cannot be negative");
        }

        if (destination > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(destination)} cannot be greater than the size of the collection");
        }

        var index = _innerList.IndexOf(item);
        Move(index, destination);
    }

    /// <summary>
    /// Moves an item from the original to the destination index.
    /// </summary>
    /// <param name="original">The original.</param>
    /// <param name="destination">The destination.</param>
    public virtual void Move(int original, int destination)
    {
        if (original < 0)
        {
            throw new ArgumentException($"{nameof(original)} cannot be negative");
        }

        if (destination < 0)
        {
            throw new ArgumentException($"{nameof(destination)} cannot be negative");
        }

        if (original > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(original)} cannot be greater than the size of the collection");
        }

        if (destination > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(destination)} cannot be greater than the size of the collection");
        }

        var item = _innerList[original];
        _innerList.RemoveAt(original);
        _innerList.Insert(destination, item);
        _changes.Add(new Change<T>(item, destination, original));
    }

    /// <summary>
    /// <para>Add a Refresh change of the item at the specified index to the list of changes.</para>
    /// <para>This is to notify downstream operators to refresh.</para>
    /// </summary>
    /// <param name="item">The item to refresh.</param>
    /// <param name="index">The index to refresh.</param>
    public void Refresh(T item, int index)
    {
        if (index < 0)
        {
            throw new ArgumentException($"{nameof(index)} cannot be negative");
        }

        if (index > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(index)} cannot be greater than the size of the collection");
        }

        var previous = _innerList[index];
        _innerList[index] = item;

        _changes.Add(new Change<T>(ListChangeReason.Refresh, item, previous, index));
    }

    /// <summary>
    /// Add a Refresh change for specified index to the list of changes.
    ///  This is to notify downstream operators to refresh.
    /// </summary>
    /// <param name="item">The item to refresh.</param>
    /// <returns>If the item is in the list, returns true.</returns>
    public bool Refresh(T item)
    {
        var index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        _changes.Add(new Change<T>(ListChangeReason.Refresh, item, index));

        return true;
    }

    /// <summary>
    /// <para>Add a Refresh change of the item at the specified index to the list of changes.</para>
    /// <para>This is to notify downstream operators to refresh.</para>
    /// </summary>
    /// <param name="index">The index to refresh.</param>
    public void RefreshAt(int index)
    {
        if (index < 0)
        {
            throw new ArgumentException($"{nameof(index)} cannot be negative");
        }

        if (index > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(index)} cannot be greater than the size of the collection");
        }

        _changes.Add(new Change<T>(ListChangeReason.Refresh, _innerList[index], index));
    }

    /// <summary>
    /// Removes the item from the collection and returns true if the item was successfully removed.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns>If the item was removed.</returns>
    public bool Remove(T item)
    {
        var index = _innerList.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveItem(index, item);
        return true;
    }

    /// <summary>
    /// Removes the item from the specified index.
    /// </summary>
    /// <param name="index">The index to remove the item at.</param>
    public void RemoveAt(int index)
    {
        if (index < 0)
        {
            throw new ArgumentException($"{nameof(index)} cannot be negative");
        }

        if (index > _innerList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{nameof(index)} cannot be greater than the size of the collection");
        }

        RemoveItem(index);
    }

    /// <summary>
    /// Removes a range of elements from the <see cref="List{T}"/>.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range of elements to remove.</param><param name="count">The number of elements to remove.</param><exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.-or-<paramref name="count"/> is less than 0.</exception><exception cref="ArgumentException"><paramref name="index"/> and <paramref name="count"/> do not denote a valid range of elements in the <see cref="List{T}"/>.</exception>
    public void RemoveRange(int index, int count)
    {
        if (index >= _innerList.Count || index + count > _innerList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var toRemove = _innerList.Skip(index).Take(count).ToList();
        if (toRemove.Count == 0)
        {
            return;
        }

        var args = new Change<T>(ListChangeReason.RemoveRange, toRemove, index);

        _changes.Add(args);
        _innerList.RemoveRange(index, count);

        OnRemoveItems(index, args.Range);
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Clears the changes (for testing).
    /// </summary>
    internal void ClearChanges() => _changes = [];

    /// <summary>
    /// Inserts an item at the specified index.
    /// </summary>
    /// <param name="index">the index where the item should be inserted.</param>
    /// <param name="item">The item to insert.</param>
    protected virtual void InsertItem(int index, T item)
    {
        if (index < 0)
        {
            throw new ArgumentException($"{nameof(index)} cannot be negative");
        }

        if (index > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(index)} cannot be greater than the size of the collection");
        }

        // attempt to batch updates as lists love to deal with ranges! (sorry if this code melts your mind)
        var last = Last;

        if (last.HasValue && last.Value.Reason == ListChangeReason.Add)
        {
            // begin a new batch if possible
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
            // check whether the new item is in the specified range
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

                if (index < range.Index)
                {
                    range.SetStartingIndex(index);
                }
            }
        }
        else
        {
            // first add, so cannot infer range
            _changes.Add(new Change<T>(ListChangeReason.Add, item, index));
        }

        // finally, add the item
        _innerList.Insert(index, item);
    }

    /// <summary>
    /// Override for custom Insert.
    /// </summary>
    /// <param name="startIndex">The starting index of the items being inserted.</param>
    /// <param name="items">The items being inserted.</param>
    protected virtual void OnInsertItems(int startIndex, IEnumerable<T> items)
    {
    }

    /// <summary>
    /// Override for custom remove.
    /// </summary>
    /// <param name="startIndex">The starting index of the items being removed.</param>
    /// <param name="items">The items being removed.</param>
    protected virtual void OnRemoveItems(int startIndex, IEnumerable<T> items)
    {
    }

    /// <summary>
    /// Override for custom Set.
    /// </summary>
    /// <param name="index">The index of the item set.</param>
    /// <param name="newItem">The new item.</param>
    /// <param name="oldItem">The old item.</param>
    protected virtual void OnSetItem(int index, T newItem, T oldItem)
    {
    }

    /// <summary>
    /// Remove the item which is at the specified index.
    /// </summary>
    /// <param name="index">The index being removed.</param>
    protected void RemoveItem(int index)
    {
        var item = _innerList[index];
        RemoveItem(index, item);
    }

    /// <summary>
    /// Removes the item from the specified index - intended for internal use only.
    /// </summary>
    /// <param name="index">The index being removed.</param>
    /// <param name="item">The item being removed.</param>
    protected virtual void RemoveItem(int index, T item)
    {
        if (index < 0)
        {
            throw new ArgumentException($"{nameof(index)} cannot be negative");
        }

        if (index > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(index)} cannot be greater than the size of the collection");
        }

        // attempt to batch updates as lists love to deal with ranges! (sorry if this code melts your mind)
        var last = Last;
        if (last.HasValue && last.Value.Reason == ListChangeReason.Remove)
        {
            // begin a new batch
            var firstOfBatch = _changes.Count - 1;
            var previousItem = last.Value.Item;

            if (index == previousItem.CurrentIndex)
            {
                _changes[firstOfBatch] = new Change<T>(ListChangeReason.RemoveRange, new[] { previousItem.Current, item }, index);
            }
            else if (index == previousItem.CurrentIndex - 1)
            {
                // Nb: double check this one as it is the same as clause above. Can it be correct?
                _changes[firstOfBatch] = new Change<T>(ListChangeReason.RemoveRange, new[] { item, previousItem.Current }, index);
            }
            else
            {
                _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
            }
        }
        else if (last.HasValue && last.Value.Reason == ListChangeReason.RemoveRange)
        {
            // add to the end of the previous batch
            var range = last.Value.Range;
            if (range.Index == index)
            {
                // removed in order
                range.Add(item);
            }
            else if (range.Index == index - 1)
            {
                _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
            }
            else if (range.Index == index + 1)
            {
                // removed in reverse order
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
            // first remove, so cannot infer range
            _changes.Add(new Change<T>(ListChangeReason.Remove, item, index));
        }

        _innerList.RemoveAt(index);
    }

    /// <summary>
    /// Replaces the element which is as the specified index wth the specified item.
    /// </summary>
    /// <param name="index">The index of the item to set.</param>
    /// <param name="item">The item to set.</param>
    protected virtual void SetItem(int index, T item)
    {
        if (index < 0)
        {
            throw new ArgumentException($"{nameof(index)} cannot be negative");
        }

        if (index > _innerList.Count)
        {
            throw new ArgumentException($"{nameof(index)} cannot be greater than the size of the collection");
        }

        var previous = _innerList[index];
        _changes.Add(new Change<T>(ListChangeReason.Replace, item, previous, index, index));
        _innerList[index] = item;

        OnSetItem(index, item, previous);
    }
}
