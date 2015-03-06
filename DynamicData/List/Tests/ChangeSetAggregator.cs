using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;


namespace DynamicData.Tests
{
    /// <summary>
    /// Aggregates all events and statistics for a changeset to help assertions when testing
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    public class ChangeSetAggregator<TObject> : IDisposable
    {
        private readonly IDisposable _disposer;
        private readonly IObservableList<TObject> _data;
        private readonly IList<IChangeSet<TObject>> _messages = new List<IChangeSet<TObject>>();
        private  ChangeSummary _summary;
        private Exception _error;


        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeSetAggregator{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        public ChangeSetAggregator(IObservable<IChangeSet<TObject>> source)
        {
            var published = source.Publish();

            _data = published.AsObservableList();
            
            var results = published.Subscribe(updates => _messages.Add(updates), ex => _error = ex);
            var summariser = published.CollectUpdateStats().Subscribe(summary => _summary = summary);
            var connected = published.Connect();

            _disposer = Disposable.Create(() =>
                                              {
                                                  _data.Dispose();
                                                  connected.Dispose();
                                                  summariser.Dispose();
                                                  results.Dispose();
                                              });
        }

        public IObservableList<TObject> Data => _data;
	    public IList<IChangeSet<TObject>> Messages => _messages;
        public ChangeSummary Summary => _summary;
        public Exception Error => _error;

	    public void Dispose()
        {
            _disposer.Dispose();
        }
    }
}