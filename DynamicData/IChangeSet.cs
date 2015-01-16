using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
    /// <summary>
    /// A collection of changes.
    /// 
    /// Changes are always published in the order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IChangeSet<TObject,  TKey> : IEnumerable<Change<TObject, TKey>>
    {
        /// <summary>
        ///     Gets the number of additions
        /// </summary>
        int Adds { get; }

        /// <summary>
        ///     Gets the number of updates
        /// </summary>
        int Updates { get; }

        /// <summary>
        ///     Gets the number of removes
        /// </summary>
        int Removes { get; }

        /// <summary>
        ///     Gets the number of requeries
        /// </summary>
        int Evaluates { get; }


        /// <summary>
        ///     Gets the number of moves
        /// </summary>
        int Moves { get; }

        /// <summary>
        ///     The total update count
        /// </summary>
        int Count { get; }
    }
}