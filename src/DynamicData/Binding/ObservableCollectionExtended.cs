// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Disposables;

namespace DynamicData.Binding;

/// <summary>
/// An override of observable collection which allows the suspension of notifications.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public class ObservableCollectionExtended<T> : ObservableCollection<T>, IObservableCollection<T>, IExtendedList<T>
{
    private bool _suspendCount;

    private bool _suspendNotifications;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionExtended{T}"/> class.
    /// </summary>
    public ObservableCollectionExtended()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionExtended{T}"/> class that contains elements copied from the specified list.
    /// </summary>
    /// <param name="list">The list from which the elements are copied.</param><exception cref="ArgumentNullException">The <paramref name="list"/> parameter cannot be null.</exception>
    public ObservableCollectionExtended(List<T> list)
        : base(list)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionExtended{T}"/> class that contains elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">The collection from which the elements are copied.</param><exception cref="ArgumentNullException">The <paramref name="collection"/> parameter cannot be null.</exception>
    public ObservableCollectionExtended(IEnumerable<T> collection)
        : base(collection)
    {
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the collection.
    /// </summary>
    /// <param name="collection">The collection whose elements should be added to the end of the List. The collection itself cannot be null, but it can contain elements that are null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection" /> is null.</exception>
    public void AddRange(IEnumerable<T> collection)
    {
        collection.ThrowArgumentNullExceptionIfNull(nameof(collection));

        foreach (var item in collection)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Inserts the elements of a collection into the <see cref="ObservableCollectionExtended{T}" /> at the specified index.
    /// </summary>
    /// <param name="collection">Inserts the items at the specified index.</param>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> is less than 0.-or-<paramref name="index" /> is greater than Count.</exception>
    public void InsertRange(IEnumerable<T> collection, int index)
    {
        collection.ThrowArgumentNullExceptionIfNull(nameof(collection));

        foreach (var item in collection)
        {
            InsertItem(index++, item);
        }
    }

    /// <summary>
    /// Clears the list and Loads the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    public void Load(IEnumerable<T> items)
    {
        items.ThrowArgumentNullExceptionIfNull(nameof(items));

        CheckReentrancy();
        Clear();

        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Removes a range of elements from the <see cref="ObservableCollectionExtended{T}"/>.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range of elements to remove.</param><param name="count">The number of elements to remove.</param><exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.-or-<paramref name="count"/> is less than 0.</exception><exception cref="ArgumentException"><paramref name="index"/> and <paramref name="count"/> do not denote a valid range of elements in the <see cref="List{T}"/>.</exception>
    public void RemoveRange(int index, int count)
    {
        for (var i = 0; i < count; i++)
        {
            RemoveAt(index);
        }
    }

    /// <summary>
    /// Suspends count notifications.
    /// </summary>
    /// <returns>A disposable when disposed will reset the count.</returns>
    public IDisposable SuspendCount()
    {
        var count = Count;
        _suspendCount = true;
        return Disposable.Create(
            () =>
            {
                _suspendCount = false;

                if (Count != count)
                {
                    OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                }
            });
    }

    /// <summary>
    /// Suspends notifications. When disposed, a reset notification is fired.
    /// </summary>
    /// <returns>A disposable when disposed will reset notifications.</returns>
    public IDisposable SuspendNotifications()
    {
        _suspendCount = true;
        _suspendNotifications = true;

        return Disposable.Create(
            () =>
            {
                _suspendCount = false;
                _suspendNotifications = false;
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            });
    }

    /// <summary>
    /// Raises the <see cref="INotifyCollectionChanged.CollectionChanged" /> event.
    /// </summary>
    /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suspendNotifications)
        {
            return;
        }

        base.OnCollectionChanged(e);
    }

    /// <summary>
    /// Raises the <see cref="INotifyPropertyChanged.PropertyChanged" /> event.
    /// </summary>
    /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        e.ThrowArgumentNullExceptionIfNull(nameof(e));

        if (_suspendCount && e.PropertyName == "Count")
        {
            return;
        }

        base.OnPropertyChanged(e);
    }
}
