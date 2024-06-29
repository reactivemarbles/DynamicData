// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// A readonly observable list, providing  observable methods
/// as well as data access methods.
/// </summary>
/// <typeparam name="T">The type of item.</typeparam>
public interface IObservableList<T> : IDisposable
    where T : notnull
{
    /// <summary>
    /// Gets the count.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets observe the count changes, starting with the initial items count.
    /// </summary>
    IObservable<int> CountChanged { get; }

    /// <summary>
    /// Gets items enumerable.
    /// </summary>
    IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Connect to the observable list and observe any changes
    /// starting with the list's initial items.
    /// </summary>
    /// <param name="predicate">The result will be filtered on the specified predicate.</param>
    /// <returns>An observable which emits the change set.</returns>
    IObservable<IChangeSet<T>> Connect(Func<T, bool>? predicate = null);

    /// <summary>
    /// Connect to the observable list and observe any changes before they are applied to the list.
    /// Unlike Connect(), the returned observable is not prepended with the lists initial items.
    /// </summary>
    /// <param name="predicate">The result will be filtered on the specified predicate.</param>
    /// <returns>An observable which emits the change set.</returns>
    IObservable<IChangeSet<T>> Preview(Func<T, bool>? predicate = null);
}
