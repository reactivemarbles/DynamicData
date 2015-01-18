using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;

namespace DynamicData.Tests
{
    public class DistinctChangeSetAggregator<TValue> : IDisposable
    {
        private readonly IDisposable _disposer;
        private readonly IObservableCache<TValue, TValue> _data;
        private readonly IList<IChangeSet<TValue, TValue>> _messages = new List<IChangeSet<TValue, TValue>>();
        private ChangeSummary _summary;
        private Exception _error;


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



        public IObservableCache<TValue, TValue> Data
        {
            get { return _data; }
        }

        public IList<IChangeSet<TValue, TValue>> Messages
        {
            get { return _messages; }
        }

        public ChangeSummary Summary
        {
            get { return _summary; }
        }

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