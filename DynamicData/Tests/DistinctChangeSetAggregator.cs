using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a distinct changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class DistinctChangeSetAggregator<TValue> : IDisposable
    {
        private readonly IDisposable _disposer;
        private readonly IObservableCache<TValue, TValue> _data;
        private readonly IList<IChangeSet<TValue, TValue>> _messages = new List<IChangeSet<TValue, TValue>>();
        private ChangeSummary _summary;
        private Exception _error;


        /// <summary>
        /// Initializes a new instance of the <see cref="DistinctChangeSetAggregator{TValue}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public DistinctChangeSetAggregator(IObservable<IDistinctChangeSet<TValue>> source)
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
        public IObservableCache<TValue, TValue> Data
        {
            get { return _data; }
        }

        /// <summary>
        /// Gets the messages.
        /// </summary>
        /// <value>
        /// The messages.
        /// </value>
        public IList<IChangeSet<TValue, TValue>> Messages
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