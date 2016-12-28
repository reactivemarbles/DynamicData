using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A readonly observable list, providing  observable methods
    /// as well as data access methods
    /// </summary>
    public interface IObservableList<T> : IDisposable
    {
        /// <summary>
        /// Connect to the observable list and observe any changes
        /// starting with the list's initial items. 
        /// </summary>
        /// <param name="predicate">The result will be filtered on the specfied predicate.</param>
        IObservable<IChangeSet<T>> Connect(Func<T, bool> predicate = null);

        /// <summary>
        /// Observe the count changes, starting with the inital items count
        /// </summary>
        IObservable<int> CountChanged { get; }

        /// <summary>
        /// Items enumerable
        /// </summary>
        IEnumerable<T> Items { get; }

        /// <summary>
        /// Gets the count.
        /// </summary>
        int Count { get; }
    }
}
