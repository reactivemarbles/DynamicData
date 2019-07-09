using System.Collections.Generic;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Represents a group which provides an update after any value within the group changes
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
    public interface IGrouping<TObject, TKey, out TGroupKey>
    {
        /// <summary>
        /// Gets the group key
        /// </summary>
        TGroupKey Key { get; }

        /// <summary>
        /// Gets the keys.
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        /// <summary>
        /// Gets the items.
        /// </summary>
        IEnumerable<TObject> Items { get; }

        /// <summary>
        /// Gets the items together with their keys
        /// </summary>
        /// <value>
        /// The key values.
        /// </value>
        IEnumerable<KeyValuePair<TKey, TObject>> KeyValues { get; }

        /// <summary>
        /// Gets the count.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup
        /// </remarks>
        /// <param name="key">The key.</param>
        Optional<TObject> Lookup(TKey key);
    }
}