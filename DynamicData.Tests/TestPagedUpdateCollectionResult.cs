using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Diagnostics;
using DynamicData.Kernel;

namespace DynamicData.Tests
{
    public class TestPagedChangeSetResult<TObject, TKey> : IDisposable
    {
        private readonly IList<IPagedChangeSet<TObject, TKey>> _messages = new List<IPagedChangeSet<TObject, TKey>>();
        private ChangeSummary _summary;
        private Exception _error;
        private readonly IDisposable _disposer;

        private readonly IObservableCache<TObject, TKey> _data;

        public TestPagedChangeSetResult(IObservable<IPagedChangeSet<TObject, TKey>> source)
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



        public IObservableCache<TObject, TKey> Data
        {
            get { return _data; }
        }

        public IList<IPagedChangeSet<TObject, TKey>> Messages
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