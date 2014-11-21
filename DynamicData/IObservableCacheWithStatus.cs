using System;

namespace DynamicData
{
    /// <summary>
    /// An observable cache decorated with an cache status observable
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IObservableCacheWithStatus<TObject, TKey> : IObservableCache<TObject, TKey> 
    {
        /// <summary>
        /// Connection status observable.
        /// </summary>
        /// <remarks>Cache is considered loaded when it has first received data</remarks>
        IObservable<ConnectionStatus> Status { get; }
    }
}