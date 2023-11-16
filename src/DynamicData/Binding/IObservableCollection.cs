// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Specialized;
using System.ComponentModel;

namespace DynamicData.Binding;

/// <summary>
/// An override of observable collection which allows the suspension of notifications.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IObservableCollection<T> : INotifyCollectionChanged, INotifyPropertyChanged, IList<T>, INotifyCollectionChangedSuspender
{
    /// <summary>
    /// Clears the list and Loads the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    void Load(IEnumerable<T> items);

    /// <summary>
    /// Moves the item at the specified index to a new location in the collection.
    /// </summary>
    /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
    /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
    void Move(int oldIndex, int newIndex);
}
