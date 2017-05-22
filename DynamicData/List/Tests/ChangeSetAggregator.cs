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
    public class ChangeSetAggregator<TObject> : IDisposable
    {
        private readonly IDisposable _disposer;
        private readonly IList<IChangeSet<TObject>> _messages = new List<IChangeSet<TObject>>();
        private ChangeSummary _summary = ChangeSummary.Empty;
        private Exception _error;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSetAggregator{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public ChangeSetAggregator(IObservable<IChangeSet<TObject>> source)
        {
            var published = source.Publish();

            Data = published.AsObservableList();

            var results = published.Subscribe(updates => _messages.Add(updates), ex => _error = ex);
            var summariser = published.CollectUpdateStats().Subscribe(summary => _summary = summary);
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
        /// A clone of the daata
        /// </summary>
        public IObservableList<TObject> Data { get; }

        /// <summary>
        /// All message received
        /// </summary>
        public IList<IChangeSet<TObject>> Messages => _messages;

        /// <summary>
        /// Gets or sets the summary.
        /// </summary>
        public ChangeSummary Summary => _summary;

        /// <summary>
        /// Gets or sets the error.
        /// </summary>
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
