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
            var connected = published.Connect();
            
            _disposer = Disposable.Create(() =>
            {
                Data.Dispose();
                connected.Dispose();
                results.Dispose();
            });
        }

        /// <summary>
        /// A clone of the data
        /// </summary>
        public IObservableList<TObject> Data { get; }

        /// <summary>
        /// All message received
        /// </summary>
        public IList<IChangeSet<TObject>> Messages => _messages;


        /// <inheritdoc />
        public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}
