// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// An editable observable list, providing  observable methods
/// as well as data access methods.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface ISourceList<T> : IObservableList<T>
    where T : notnull
{
    /// <summary>
    /// Edit the inner list within the list's internal locking mechanism.
    /// </summary>
    /// <param name="updateAction">The update action.</param>
    void Edit(Action<IExtendedList<T>> updateAction);
}
