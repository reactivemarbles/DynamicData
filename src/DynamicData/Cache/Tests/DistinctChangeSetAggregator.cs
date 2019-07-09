using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a distinct changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class DistinctChangeSetAggregator<TValue> : IDisposable
    {
        private readonly IDisposable _disposer;
        private ChangeSummary _summary = ChangeSummary.Empty;
        private Exception _error;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistinctChangeSetAggregator{TValue}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public DistinctChangeSetAggregator(IObservable<IDistinctChangeSet<TValue>> source)
        {
            var published = source.Publish();

            var error = published.Subscribe(updates => { }, ex => _error = ex);
            var results = published.Subscribe(updates => Messages.Add(updates));
            Data = published.AsObservableCache();
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
        public IObservableCache<TValue, TValue> Data { get; }

        /// <summary>
        /// Gets the messages.
        /// </summary>
        public IList<IChangeSet<TValue, TValue>> Messages { get; } = new List<IChangeSet<TValue, TValue>>();

        /// <summary>
        /// Gets the summary.
        /// </summary>
        public ChangeSummary Summary => _summary;

        /// <summary>
        /// Gets the error.
        /// </summary>
        /// <value>
        /// The error.
        /// </value>
        public Exception Error => _error;

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}
