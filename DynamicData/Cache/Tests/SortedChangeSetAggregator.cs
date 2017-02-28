using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a sorted changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class SortedChangeSetAggregator<TObject, TKey> : IDisposable
    {
        private readonly IDisposable _disposer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SortedChangeSetAggregator{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public SortedChangeSetAggregator(IObservable<ISortedChangeSet<TObject, TKey>> source)
        {
            var published = source.Publish();

            var error = published.Subscribe(updates => { }, ex => Error = ex);
            var results = published.Subscribe(updates => Messages.Add(updates));
            Data = published.AsObservableCache();
            var summariser = published.CollectUpdateStats().Subscribe(summary => Summary = summary);

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
        /// The data of the steam cached inorder to apply assertions
        /// </summary>
        public IObservableCache<TObject, TKey> Data { get; }

        /// <summary>
        /// Record of all received messages.
        /// </summary>
        /// <value>
        /// The messages.
        /// </value>
        public IList<ISortedChangeSet<TObject, TKey>> Messages { get; } = new List<ISortedChangeSet<TObject, TKey>>();

        /// <summary>
        /// The aggregated change summary.
        /// </summary>
        /// <value>
        /// The summary.
        /// </value>
        public ChangeSummary Summary { get; private set; } = ChangeSummary.Empty;

        /// <summary>
        /// Gets and error.
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}
