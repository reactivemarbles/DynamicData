using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
    /// <summary>
    /// A cache for observing and querying in memory data
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IConnectableCache<TObject, TKey>
    {
        /// <summary>
        /// Returns an observable of any changes which match the specified key.  The sequence starts with the inital item in the cache (if there is one).
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        IObservable<Change<TObject, TKey>> Watch(TKey key);

        /// <summary>
        /// Returns a observable of cache changes preceeded with the initital cache state
        /// </summary>
        /// <returns></returns>
        IObservable<IChangeSet<TObject, TKey>> Connect();

        /// <summary>
        /// Returns a filtered changeset of cache changes preceeded with the initial state
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <returns></returns>
        IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter);

        /// <summary>
        /// A count changed observable starting with the current count
        /// </summary>
        IObservable<int> CountChanged { get; }
    }

    /// <summary>
    ///   /// A cache for observing and querying in memory data. With additional data access operators
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IObservableCache<TObject, TKey> : IConnectableCache<TObject, TKey>, IDisposable
    {
        /// <summary>
        /// Gets the keys
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        /// <summary>
        /// Gets the Items
        /// </summary>
        IEnumerable<TObject> Items { get; }

        /// <summary>
        /// Gets the key value pairs
        /// </summary>
        IEnumerable<KeyValuePair<TKey, TObject>> KeyValues { get; }

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup
        /// </remarks>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        Optional<TObject> Lookup(TKey key);

        /// <summary>
        /// The total count of cached items
        /// </summary>
        int Count { get; }
    }
}
