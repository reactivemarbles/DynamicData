using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a virtual changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class VirtualChangeSetAggregator<TObject, TKey> : IDisposable
    {
        private readonly IList<IVirtualChangeSet<TObject, TKey>> _messages = new List<IVirtualChangeSet<TObject, TKey>>();
        private ChangeSummary _summary;
        private Exception _error;
        private readonly IDisposable _disposer;

        private readonly IObservableCache<TObject, TKey> _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualChangeSetAggregator{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public VirtualChangeSetAggregator(IObservable<IVirtualChangeSet<TObject, TKey>> source)
        {
            var published = source.Publish();

            var error = published.Subscribe(updates => { }, ex => _error = ex);
            var results = published.Subscribe(updates => _messages.Add(updates));
            _data = published.AsObservableCache();
            var summariser = published.CollectUpdateStats().Subscribe(summary => _summary = summary);

            var connected = published.Connect();
            _disposer = Disposable.Create(() =>
                                              {
                                                  connected.Dispose();
                                                  summariser.Dispose();
                                                  results.Dispose();
                                                  error.Dispose();
                                              });
        }



        /// <summary>
        /// Gets the data.
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        public IObservableCache<TObject, TKey> Data
        {
            get { return _data; }
        }

        /// <summary>
        /// Gets the messages.
        /// </summary>
        /// <value>
        /// The messages.
        /// </value>
        public IList<IVirtualChangeSet<TObject, TKey>> Messages
        {
            get { return _messages; }
        }

        /// <summary>
        /// Gets the summary.
        /// </summary>
        /// <value>
        /// The summary.
        /// </value>
        public ChangeSummary Summary
        {
            get { return _summary; }
        }

        /// <summary>
        /// Gets the error.
        /// </summary>
        /// <value>
        /// The error.
        /// </value>
        public Exception Error
        {
            get { return _error; }
        }

        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}