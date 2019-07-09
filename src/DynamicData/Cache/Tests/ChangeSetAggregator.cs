using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

// ReSharper disable once CheckNamespace
namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class ChangeSetAggregator<TObject, TKey> : IDisposable
    {
        private readonly IDisposable _disposer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSetAggregator{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public ChangeSetAggregator(IObservable<IChangeSet<TObject, TKey>> source)
        {
            var published = source.Publish();

            Data = published.AsObservableCache();

            var results = published.Subscribe(updates => Messages.Add(updates), ex => Error = ex);
            var summariser = published.CollectUpdateStats().Subscribe(summary => Summary = summary);
            var connected = published.Connect();

            _disposer = Disposable.Create(() =>
            {
                Data.Dispose();
                connected.Dispose();
                summariser.Dispose();
                results.Dispose();
            });
        }

        /// <summary>
        /// Gets the data.
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        public IObservableCache<TObject, TKey> Data { get; }

        /// <summary>
        /// Gets the messages.
        /// </summary>
        /// <value>
        /// The messages.
        /// </value>
        public IList<IChangeSet<TObject, TKey>> Messages { get; } = new List<IChangeSet<TObject, TKey>>();

        /// <summary>
        /// Gets the summary.
        /// </summary>
        /// <value>
        /// The summary.
        /// </value>
        public ChangeSummary Summary { get; private set; } = ChangeSummary.Empty;

        /// <summary>
        /// Gets the error.
        /// </summary>
        /// <value>
        /// The error.
        /// </value>
        public Exception Error { get; private set; }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}
