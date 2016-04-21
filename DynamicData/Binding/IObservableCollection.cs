using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DynamicData.Binding
{
    /// <summary>
    /// An override of observable collection which allows the suspension of notifications
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IObservableCollection<T> : INotifyCollectionChanged,
                                                INotifyPropertyChanged,
                                                IList<T>
    {
        /// <summary>
        /// Suspends notifications. When disposed, a reset notification is fired
        /// </summary>
        /// <returns></returns>
        IDisposable SuspendNotifications();

        /// <summary>
        /// Suspends count notifications
        /// </summary>
        /// <returns></returns>
        IDisposable SuspendCount();

        /// <summary>
        /// Moves the item at the specified index to a new location in the collection.
        /// </summary>
        /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
        /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
        void Move(int oldIndex, int newIndex);

        /// <summary>
        /// Clears the list and Loads the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void Load(IEnumerable<T> items);
    }
}
