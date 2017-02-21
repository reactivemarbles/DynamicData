using System.Collections.Generic;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal interface IIndexCalculator<TObject, TKey>
    {
        /// <summary>
        /// Initialises the specified changes.
        /// </summary>
        /// <param name="cache">The cache.</param>
        /// <returns></returns>
        IChangeSet<TObject, TKey> Load(ChangeAwareCache<TObject, TKey> cache);

        /// <summary>
        /// Dynamic calculation of changed items which produce a result which can be enumerated through in order
        /// </summary>
        /// <returns></returns>
        IChangeSet<TObject, TKey> Calculate(IChangeSet<TObject, TKey> changes);

        /// <summary>
        /// Changes the comparer.
        /// </summary>
        /// <param name="comparer">The comparer.</param>
        /// <returns></returns>
        IChangeSet<TObject, TKey> ChangeComparer(KeyValueComparer<TObject, TKey> comparer);

        /// <summary>
        /// Reorders the current list.  Required when the list is sorted on mutable values
        /// </summary>
        /// <returns></returns>
        IChangeSet<TObject, TKey> Reorder();

        /// <summary>
        /// Gets the comparer.
        /// </summary>
        /// <value>
        /// The comparer.
        /// </value>
        IComparer<KeyValuePair<TKey, TObject>> Comparer { get; }

        /// <summary>
        /// Gets the list.
        /// </summary>
        List<KeyValuePair<TKey, TObject>> List { get; }
    }
}
