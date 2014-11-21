using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Kernel
{
    internal class ObservableCacheWithStatus<TObject, TKey> : DisposableBase, IObservableCacheWithStatus<TObject, TKey>
    {
        private readonly IObservableCache<TObject, TKey> _source;
        private readonly IObservable<ConnectionStatus> _status;
        private readonly IDisposable _disposer;

        public ObservableCacheWithStatus(IObservableCache<TObject, TKey> source)
        {
            _source = source;
            _status = _source.Connect().MonitorStatus().Replay(1).RefCount();

            _disposer = new CompositeDisposable(_source);
        }

        public IObservable<ConnectionStatus> Status
        {
            get { return _status; }
        }

        #region Overrides of DisposableBase

        /// <summary>
        ///put here the code to dispose all managed and unmanaged resources
        /// </summary>
        protected override void CleanUp()
        {
            _disposer.Dispose();
        }

        #endregion

        #region Delegated members


        /// <summary>
        /// Watches updates from a single item using the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _source.Watch(key);
        }

        /// <summary>
        /// Returns a stream of cache updates preceeded with the initital cache state
        /// </summary>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return _source.Connect();
        }

        /// <summary>
        /// Returns a filtered stream of cache updates preceeded with the initital filtered state
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <param name="parallelisationOptions">Option to parallise the filter operation  Only applies if the filter parameter is not null</param>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter,
            ParallelisationOptions parallelisationOptions = null)
        {
            return _source.Connect(filter, parallelisationOptions);
        }

        /// <summary>
        /// Gets the keys
        /// </summary>
        public IEnumerable<TKey> Keys
        {
            get { return _source.Keys; }
        }

        /// <summary>
        /// Gets the key value pairs
        /// </summary>
        public IEnumerable<KeyValue<TObject, TKey>> KeyValues
        {
            get { return _source.KeyValues; }
        }

        /// <summary>
        /// Gets the Items
        /// </summary>
        public IEnumerable<TObject> Items
        {
            get { return _source.Items; }
        }

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup
        /// </remarks>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public Optional<TObject> Lookup(TKey key)
        {
            return _source.Lookup(key);
        }

        /// <summary>
        /// The total count of cached items
        /// </summary>
        public int Count
        {
            get { return _source.Count; }
        }



        #endregion

    }
}