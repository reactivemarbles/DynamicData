using System;

namespace DynamicData.Experimental
{
    /// <summary>
    /// A specialisation of the SourceList which is optimised for watching individual items
    /// </summary>
    public interface IWatcher<TObject, TKey> : IDisposable
    {
        /// <summary>
        /// Watches updates which match the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        IObservable<Change<TObject, TKey>> Watch(TKey key);
    }
}
