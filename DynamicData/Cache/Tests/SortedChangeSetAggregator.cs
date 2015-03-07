using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a sorted changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public class SortedChangeSetAggregator<TObject, TKey> : IDisposable
    {
        private readonly IList<ISortedChangeSet<TObject, TKey>> _messages = new List<ISortedChangeSet<TObject, TKey>>();
        private ChangeSummary _summary;
        private Exception _error;
        private readonly IDisposable _disposer;

        private readonly IObservableCache<TObject, TKey> _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="SortedChangeSetAggregator{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public SortedChangeSetAggregator(IObservable<ISortedChangeSet<TObject, TKey>> source)
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



        public IObservableCache<TObject, TKey> Data => _data;

	    public IList<ISortedChangeSet<TObject, TKey>> Messages => _messages;

	    public ChangeSummary Summary => _summary;

	    public Exception Error => _error;

	    public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}